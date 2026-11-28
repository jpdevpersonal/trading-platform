using System.Data;
using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application;
using TradingAssistant.Domain;

namespace TradingAssistant.Infrastructure;

public sealed class DailyPriceRow
{
    public Guid InstrumentId { get; set; }
    public DateOnly TradingDate { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal Volume { get; set; }
    public string Source { get; set; } = DailyPrices.Source;
    public string PriceBasis { get; set; } = DailyPrices.Basis;

    public DailyBar ToBar() => new(TradingDate, Open, High, Low, Close, Volume);

    public void Set(DailyBar bar)
    {
        TradingDate = bar.TradingDate;
        Open = bar.Open;
        High = bar.High;
        Low = bar.Low;
        Close = bar.Close;
        Volume = bar.Volume;
    }
}

public sealed class DailyPriceRefresh
{
    public Guid InstrumentId { get; set; }
    public string Source { get; set; } = DailyPrices.Source;
    public string PriceBasis { get; set; } = DailyPrices.Basis;
    public DateTime RefreshedAtUtc { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
}

public sealed class DailyPriceStore(TradingContext context, TimeProvider clock) : IDailyPriceStore
{
    public async Task<StoredPrices> ReadAsync(Guid instrumentId, PriceRange range, CancellationToken token)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
        var rows = await context.DailyPrices.AsNoTracking()
            .Where(row => row.InstrumentId == instrumentId && row.TradingDate >= range.From && row.TradingDate <= range.To)
            .OrderByDescending(row => row.TradingDate).ToListAsync(token);
        var refresh = await context.DailyPriceRefreshes.AsNoTracking().SingleOrDefaultAsync(row => row.InstrumentId == instrumentId, token);
        await transaction.CommitAsync(token);
        return new(rows.Select(row => row.ToBar()).ToArray(), refresh is null ? null :
            new(refresh.Source, refresh.PriceBasis, refresh.RefreshedAtUtc, refresh.From, refresh.To));
    }

    public async Task<PriceImport> SaveAsync(Guid instrumentId, PriceRange range, IReadOnlyList<DailyBar> bars, CancellationToken token)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(token);
        // Lock the stable parent even for a first/empty import. Every writer compares after this lock.
        await context.Instruments.FromSqlInterpolated($"""SELECT * FROM "Instruments" WHERE "Id" = {instrumentId} FOR UPDATE""")
            .AsNoTracking().SingleAsync(token);
        var refresh = await context.DailyPriceRefreshes.SingleOrDefaultAsync(row => row.InstrumentId == instrumentId, token);
        var now = clock.GetUtcNow().UtcDateTime;
        now = new DateTime(now.Ticks - now.Ticks % 10, DateTimeKind.Utc);
        if (refresh is not null && now <= refresh.RefreshedAtUtc)
            now = refresh.RefreshedAtUtc.AddTicks(10);

        var existing = await context.DailyPrices.Where(row => row.InstrumentId == instrumentId)
            .ToDictionaryAsync(row => row.TradingDate, token);
        if (existing.Values.Any(row => row.Source != DailyPrices.Source || row.PriceBasis != DailyPrices.Basis)
            || (refresh is not null && (refresh.Source != DailyPrices.Source || refresh.PriceBasis != DailyPrices.Basis)))
            throw new InvalidOperationException("Stored daily series provenance conflicts with the import.");
        var inserted = 0;
        var updated = 0;
        var unchanged = 0;
        foreach (var bar in bars)
        {
            if (!existing.TryGetValue(bar.TradingDate, out var row))
            {
                row = new DailyPriceRow { InstrumentId = instrumentId };
                row.Set(bar);
                context.DailyPrices.Add(row);
                inserted++;
            }
            else if (row.ToBar() == bar) unchanged++;
            else
            {
                row.Set(bar);
                updated++;
            }
        }
        if (refresh is null)
        {
            refresh = new DailyPriceRefresh { InstrumentId = instrumentId };
            context.DailyPriceRefreshes.Add(refresh);
        }
        refresh.RefreshedAtUtc = now;
        refresh.From = range.From;
        refresh.To = range.To;
        await context.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return new(instrumentId, DailyPrices.Source, range.From, range.To, inserted, updated, unchanged, now);
    }
}
