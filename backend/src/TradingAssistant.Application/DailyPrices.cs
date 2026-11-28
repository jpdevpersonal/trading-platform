using TradingAssistant.Domain;

namespace TradingAssistant.Application;

public enum PriceFailureKind { InvalidRange, NotFound, Ineligible, Unavailable, Throttled, Provider, Timeout }

public sealed class PriceFailure(PriceFailureKind kind, string message, int? retryAfterSeconds = null) : Exception(message)
{
    public PriceFailureKind Kind { get; } = kind;
    public int? RetryAfterSeconds { get; } = retryAfterSeconds;
}

public sealed record PriceRange(DateOnly From, DateOnly To)
{
    public static readonly TimeZoneInfo MarketZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    public static DateOnly MarketDate(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, MarketZone).DateTime);

    public void Validate(bool importing, TimeProvider clock)
    {
        if (To < From || To.DayNumber - From.DayNumber >= 366)
            throw new PriceFailure(PriceFailureKind.InvalidRange, "Supply from/to in order, spanning at most 366 calendar days inclusive.");
        var today = MarketDate(clock.GetUtcNow());
        if (importing && (To >= today || From < today.AddYears(-2)))
            throw new PriceFailure(PriceFailureKind.InvalidRange, "Import from within the last two calendar years through yesterday in America/New_York.");
    }
}

public interface IDailyPriceProvider
{
    bool CanImport(Instrument instrument);
    Task<IReadOnlyList<DailyBar>> FetchAsync(Instrument instrument, PriceRange range, CancellationToken token);
}

public sealed record PriceRefresh(string Source, string PriceBasis, DateTime RefreshedAtUtc, DateOnly From, DateOnly To);
public sealed record StoredPrices(IReadOnlyList<DailyBar> Items, PriceRefresh? Refresh);
public sealed record PriceHistory(Guid InstrumentId, string Currency, bool CanImport, StoredPrices Stored);
public sealed record PriceImport(Guid InstrumentId, string Source, DateOnly From, DateOnly To,
    int Inserted, int Updated, int Unchanged, DateTime LastRefreshedAtUtc);

public interface IDailyPriceStore
{
    Task<StoredPrices> ReadAsync(Guid instrumentId, PriceRange range, CancellationToken token);
    Task<PriceImport> SaveAsync(Guid instrumentId, PriceRange range, IReadOnlyList<DailyBar> bars, CancellationToken token);
}

public sealed class DailyPrices(IInstrumentReader instruments, IDailyPriceProvider provider, IDailyPriceStore store, TimeProvider clock)
{
    public const string Source = "Massive";
    public const string Basis = "raw";

    public async Task<PriceHistory> ReadAsync(Guid id, PriceRange range, CancellationToken token)
    {
        range.Validate(false, clock);
        var instrument = await FindAsync(id, token);
        return new(id, instrument.Currency, provider.CanImport(instrument), await store.ReadAsync(id, range, token));
    }

    public async Task<PriceImport> ImportAsync(Guid id, PriceRange range, CancellationToken token)
    {
        range.Validate(true, clock);
        var instrument = await FindAsync(id, token);
        if (!provider.CanImport(instrument))
            throw new PriceFailure(PriceFailureKind.Ineligible, "Only non-sample XNAS AAPL/MSFT USD listings can be imported.");
        var bars = await provider.FetchAsync(instrument, range, token);
        if (bars.Any(bar => bar.TradingDate < range.From || bar.TradingDate > range.To)
            || bars.Select(bar => bar.TradingDate).Distinct().Count() != bars.Count)
            throw new PriceFailure(PriceFailureKind.Provider, "The provider returned invalid daily sessions. Retry later.");
        token.ThrowIfCancellationRequested();
        return await store.SaveAsync(id, range, bars, token);
    }

    private async Task<Instrument> FindAsync(Guid id, CancellationToken token) =>
        await instruments.FindAsync(id, token) ?? throw new PriceFailure(PriceFailureKind.NotFound, "Instrument not found.");
}
