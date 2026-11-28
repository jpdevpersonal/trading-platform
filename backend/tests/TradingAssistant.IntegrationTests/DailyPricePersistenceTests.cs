using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using TradingAssistant.Application;
using TradingAssistant.Domain;
using TradingAssistant.Infrastructure;
using Xunit;

namespace TradingAssistant.IntegrationTests;

public class DailyPricePersistenceTests
{
    private static readonly Guid Apple = Guid.Parse(DailyPriceApiTests.AppleId);
    private static readonly PriceRange Range = new(new(2026, 11, 23), new(2026, 11, 24));
    private static DailyBar Bar(int day, decimal close = 11.0000000001m) => new(new(2026, 11, day), 10.1234567891m, 12m, 9m, close, 123.0000000001m);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FreshAndPopulatedM03UpgradeAreExplicitAdditiveAndRepeatable(bool upgrade)
    {
        await using var database = await PostgresDatabase.CreateAsync();
        var before = "";
        if (upgrade)
        {
            await using var m03 = database.CreateContext();
            await m03.GetService<IMigrator>().MigrateAsync("20261126204038_InitialInstruments");
            await database.ImportAsync();
            before = await database.ScalarAsync<string>("""SELECT json_agg(i ORDER BY "Id")::text FROM "Instruments" i""");
        }
        await database.MigrateAsync();
        await database.MigrateAsync();
        await using var context = database.CreateContext();
        Assert.Equal(2, (await context.Database.GetAppliedMigrationsAsync()).Count());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Empty(await context.DailyPrices.ToListAsync());
        Assert.Empty(await context.DailyPriceRefreshes.ToListAsync());
        if (upgrade)
            Assert.Equal(before, await database.ScalarAsync<string>("""SELECT json_agg(i ORDER BY "Id")::text FROM "Instruments" i"""));
        else Assert.Empty(await context.Instruments.ToListAsync());
        Assert.Equal(5L, await database.ScalarAsync<long>("""
            SELECT count(*) FROM information_schema.columns WHERE table_schema = current_schema()
            AND table_name = 'DailyPrices' AND numeric_precision = 28 AND numeric_scale = 10
            """));
        Assert.Equal("date", await database.ScalarAsync<string>("""
            SELECT data_type FROM information_schema.columns WHERE table_schema = current_schema()
            AND table_name = 'DailyPrices' AND column_name = 'TradingDate'
            """));
        Assert.Equal("timestamp with time zone", await database.ScalarAsync<string>("""
            SELECT data_type FROM information_schema.columns WHERE table_schema = current_schema()
            AND table_name = 'DailyPriceRefreshes' AND column_name = 'RefreshedAtUtc'
            """));
    }

    [Fact]
    public async Task RealListingSetupIsRepeatableAndReusesCompatibleUuidWithoutPrices()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await database.MigrateAsync();
        await DailyPriceApiTests.SeedAsync(database);
        await DailyPriceApiTests.SeedAsync(database);
        await using (var context = database.CreateContext())
        {
            var rows = await context.Instruments.OrderBy(row => row.Symbol).ToListAsync();
            Assert.Equal(["AAPL", "MSFT"], rows.Select(row => row.Symbol));
            Assert.All(rows, row =>
            {
                Assert.Equal("XNAS", row.ExchangeMic);
                Assert.Equal("USD", row.Currency);
                Assert.False(row.IsSample);
            });
            Assert.Empty(await context.DailyPrices.ToListAsync());
            Assert.Empty(await context.DailyPriceRefreshes.ToListAsync());
        }
        await database.ExecuteAsync("""DELETE FROM "Instruments" """);
        var id = Guid.NewGuid();
        await using (var context = database.CreateContext())
        {
            context.Instruments.Add(new(id, "AAPL", "XNAS", "Retain this name", "USD", false));
            await context.SaveChangesAsync();
        }
        await DailyPriceApiTests.SeedAsync(database);
        await DailyPriceApiTests.SeedAsync(database);
        await using var independent = database.CreateContext();
        Assert.Equal(2, await independent.Instruments.CountAsync());
        Assert.Equal("Retain this name", (await independent.Instruments.SingleAsync(row => row.Id == id)).Name);
    }

    [Theory]
    [InlineData("OTHER", "USD", false, true)]
    [InlineData("MSFT", "EUR", false, true)]
    [InlineData("MSFT", "USD", true, true)]
    [InlineData("MSFT", "EUR", false, false)]
    [InlineData("MSFT", "USD", true, false)]
    public async Task ListingConflictRollsBackEarlierListingAtomically(string symbol, string currency, bool sample, bool reserved)
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await database.MigrateAsync();
        await using (var context = database.CreateContext())
        {
            context.Instruments.Add(new(reserved ? Guid.Parse(DailyPriceApiTests.MicrosoftId) : Guid.NewGuid(), symbol, "XNAS", "Existing", currency, sample));
            await context.SaveChangesAsync();
        }
        var failure = await Assert.ThrowsAsync<PostgresException>(() => DailyPriceApiTests.SeedAsync(database));
        Assert.Equal("P0001", failure.SqlState);
        await using var independent = database.CreateContext();
        Assert.Equal(symbol, (await independent.Instruments.SingleAsync()).Symbol);
    }

    [Fact]
    public async Task DatabaseEnforcesCompositeKeyForeignKeyAndExactDecimalRoundTrips()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await database.MigrateAsync();
        await DailyPriceApiTests.SeedAsync(database);
        await using (var context = database.CreateContext())
            await new DailyPriceStore(context, new PriceClock()).SaveAsync(Apple, Range, [Bar(23)], default);
        await using (var independent = database.CreateContext())
        {
            Assert.Equal(Bar(23), (await independent.DailyPrices.SingleAsync()).ToBar());
        }
        foreach (var (id, code) in new[] { (Apple, PostgresErrorCodes.UniqueViolation), (Guid.NewGuid(), PostgresErrorCodes.ForeignKeyViolation) })
        {
            await using var context = database.CreateContext();
            var duplicate = new DailyPriceRow { InstrumentId = id };
            duplicate.Set(Bar(23));
            context.DailyPrices.Add(duplicate);
            var failure = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            Assert.Equal(code, Assert.IsType<PostgresException>(failure.InnerException).SqlState);
        }
    }

    [Fact]
    public async Task DeferredCommitFailureRollsBackCorrectionsNewRowsAndRefreshTogether()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await database.MigrateAsync();
        await DailyPriceApiTests.SeedAsync(database);
        StoredPrices before;
        await using (var context = database.CreateContext())
        {
            var store = new DailyPriceStore(context, new PriceClock());
            await store.SaveAsync(Apple, Range, [Bar(23)], default);
            before = await store.ReadAsync(Apple, Range, default);
        }
        await database.ExecuteAsync("""
            CREATE FUNCTION fail_refresh() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'synthetic write failure'; END $$;
            CREATE CONSTRAINT TRIGGER fail_refresh_at_commit AFTER UPDATE ON "DailyPriceRefreshes"
            DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION fail_refresh();
            """);
        await using (var context = database.CreateContext())
            await Assert.ThrowsAsync<PostgresException>(() =>
                new DailyPriceStore(context, new PriceClock()).SaveAsync(Apple, Range, [Bar(23, 11.5m), Bar(24)], default));
        await using (var independent = database.CreateContext())
        {
            var after = await new DailyPriceStore(independent, new PriceClock()).ReadAsync(Apple, Range, default);
            Assert.Equal(before.Refresh, after.Refresh);
            Assert.Equal(before.Items, after.Items);
        }
        await database.ExecuteAsync("""DROP TRIGGER fail_refresh_at_commit ON "DailyPriceRefreshes"; DROP FUNCTION fail_refresh();""");
        await using var retry = database.CreateContext();
        var summary = await new DailyPriceStore(retry, new PriceClock()).SaveAsync(Apple, Range, [Bar(23, 11.5m), Bar(24)], default);
        Assert.Equal((1, 1, 0), (summary.Inserted, summary.Updated, summary.Unchanged));
    }

    [Fact]
    public async Task CancellationWaitingForWriterLockLeavesNoPartialImport()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await database.MigrateAsync();
        await DailyPriceApiTests.SeedAsync(database);
        await using var owner = database.CreateContext();
        await using var transaction = await owner.Database.BeginTransactionAsync();
        await owner.Database.ExecuteSqlInterpolatedAsync($"""SELECT 1 FROM "Instruments" WHERE "Id" = {Apple} FOR UPDATE""");
        await using var cancelled = database.CreateContext();
        using var token = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new DailyPriceStore(cancelled, new PriceClock()).SaveAsync(Apple, Range, [Bar(23)], token.Token));
        await transaction.RollbackAsync();
        await using var independent = database.CreateContext();
        Assert.Empty(await independent.DailyPrices.ToListAsync());
        Assert.Empty(await independent.DailyPriceRefreshes.ToListAsync());
    }

    [Fact]
    public async Task ReadReturnsBarsAndMetadataFromOneSnapshotAcrossConcurrentCommit()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await database.MigrateAsync();
        await DailyPriceApiTests.SeedAsync(database);
        PriceImport first;
        await using (var writer = database.CreateContext())
            first = await new DailyPriceStore(writer, new PriceClock()).SaveAsync(Apple, Range, [Bar(23)], default);
        var gate = new ReadGate();
        await using var reader = new TradingContext(new DbContextOptionsBuilder<TradingContext>()
            .UseNpgsql(database.ConnectionString).AddInterceptors(gate).Options);
        var reading = new DailyPriceStore(reader, new PriceClock()).ReadAsync(Apple, Range, default);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            await using var writer = database.CreateContext();
            await new DailyPriceStore(writer, new PriceClock()).SaveAsync(Apple, Range, [Bar(23, 11.5m), Bar(24)], default);
        }
        finally { gate.Release.SetResult(); }
        var snapshot = await reading;
        Assert.Equal(Bar(23), Assert.Single(snapshot.Items));
        Assert.Equal(first.LastRefreshedAtUtc, snapshot.Refresh!.RefreshedAtUtc);
    }

    private sealed class ReadGate : DbCommandInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("DailyPriceRefreshes", StringComparison.Ordinal))
            {
                Entered.SetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }
            return result;
        }
    }
}
