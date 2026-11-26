using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TradingAssistant.Application;
using TradingAssistant.Domain;
using TradingAssistant.Infrastructure;
using Xunit;

namespace TradingAssistant.IntegrationTests;

public class InstrumentPersistenceTests
{
    [Fact]
    public async Task MigrationEnforcesRequiredColumnsLengthsAndIdentity()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await database.MigrateAsync();
        Assert.Equal(6L, await database.ScalarAsync<long>("""
            SELECT count(*) FROM information_schema.columns
            WHERE table_schema = current_schema() AND table_name = 'Instruments' AND is_nullable = 'NO'
            """));
        Assert.Equal("Id:uuid,Symbol:32,ExchangeMic:4,Name:200,Currency:3,IsSample:boolean",
            await database.ScalarAsync<string>("""
                SELECT string_agg(column_name || ':' || coalesce(character_maximum_length::text, data_type), ',' ORDER BY ordinal_position)
                FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = 'Instruments'
                """));
        await database.ImportAsync();
        foreach (var (id, symbol, mic, constraint) in new[]
        {
            ("10000000-0000-0000-0000-000000000001", "OTHER", "XZZZ", "PK_Instruments"),
            (Guid.NewGuid().ToString(), "S-AO", "XAAA", "IX_Instruments_ExchangeMic_Symbol")
        })
        {
            await using var context = database.CreateContext();
            context.Instruments.Add(new Instrument(Guid.Parse(id), symbol, mic, "Duplicate", "USD", false));
            var failure = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            var postgres = Assert.IsType<PostgresException>(failure.InnerException);
            Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
            Assert.Equal(constraint, postgres.ConstraintName);
        }
        Assert.Equal(2L, await database.ScalarAsync<long>("""SELECT count(*) FROM "Instruments" WHERE "Symbol" = 'S-AO'"""));
    }

    [Fact]
    public async Task DemoImportIsIdempotentAndPreservesAnExistingLogicalRow()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await database.MigrateAsync();
        await database.ImportAsync();
        await database.ImportAsync();
        await using (var context = database.CreateContext())
        {
            var instruments = await context.Instruments.ToListAsync();
            Assert.Equal(3, instruments.Count);
            Assert.All(instruments, instrument => Assert.True(instrument.IsSample));
        }

        var existingId = Guid.NewGuid();
        await database.ExecuteAsync("""DELETE FROM "Instruments" """);
        await using (var context = database.CreateContext())
        {
            context.Instruments.Add(new Instrument(existingId, "S-AO", "XAAA", "Preserve me", "GBP", false));
            await context.SaveChangesAsync();
        }
        await database.ImportAsync();
        await database.ImportAsync();
        await using (var context = database.CreateContext())
        {
            Assert.Equal(3, await context.Instruments.CountAsync());
            var retained = await context.Instruments.SingleAsync(instrument => instrument.Id == existingId);
            Assert.Equal("Preserve me", retained.Name);
            Assert.Equal("GBP", retained.Currency);
            Assert.False(retained.IsSample);
            Assert.False(await context.Instruments.AnyAsync(instrument => instrument.Id == Guid.Parse("10000000-0000-0000-0000-000000000001")));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DemoUuidCollisionRollsBackEvenWhenLogicalRowWouldBeSkipped(bool logicalRowExists)
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await database.MigrateAsync();
        await using (var context = database.CreateContext())
        {
            context.Instruments.Add(new Instrument(Guid.Parse("10000000-0000-0000-0000-000000000003"),
                "OTHER", "XZZZ", "Existing identifier", "GBP", false));
            if (logicalRowExists)
            {
                context.Instruments.Add(new Instrument(Guid.NewGuid(), "S-AO", "XBBB", "Existing logical row", "GBP", false));
            }
            await context.SaveChangesAsync();
        }
        var failure = await Assert.ThrowsAsync<PostgresException>(() => database.ImportAsync());
        Assert.Equal("P0001", failure.SqlState);
        await using var independent = database.CreateContext();
        var remaining = await independent.Instruments.ToListAsync();
        Assert.Equal(logicalRowExists ? 2 : 1, remaining.Count);
        Assert.All(remaining, instrument => Assert.False(instrument.IsSample));
        Assert.DoesNotContain(remaining, instrument => instrument.ExchangeMic == "XAAA");
    }

    [Fact]
    public async Task ReadsAreIndependentUntrackedAndCancellableWithBoundedTimeouts()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await database.MigrateAsync();
        await database.ImportAsync();
        using var api = new TestApi(database.ConnectionString);
        using var scope = api.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TradingContext>();
        var catalogue = scope.ServiceProvider.GetRequiredService<InstrumentCatalogue>();
        var page = await catalogue.ListAsync(1, 1, CancellationToken.None);
        Assert.Equal("S-BG", Assert.Single(page).Symbol);
        var detail = await catalogue.FindAsync(page[0].Id, CancellationToken.None);
        Assert.NotNull(detail);
        Assert.NotSame(page[0], detail);
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.Equal(5, context.Database.GetCommandTimeout());
        Assert.InRange(new NpgsqlConnectionStringBuilder(context.Database.GetConnectionString()).Timeout, 1, 5);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => catalogue.ListAsync(0, 50, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => catalogue.FindAsync(page[0].Id, cancellation.Token));
    }
}
