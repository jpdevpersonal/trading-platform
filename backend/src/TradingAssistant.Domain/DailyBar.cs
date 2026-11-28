namespace TradingAssistant.Domain;

public sealed record DailyBar
{
    public DailyBar(DateOnly tradingDate, decimal open, decimal high, decimal low, decimal close, decimal volume)
    {
        if (new[] { open, high, low, close, volume }.Any(value =>
            Math.Abs(value) > 999999999999999999.9999999999m || decimal.Round(value, 10) != value))
        {
            throw new ArgumentException("Daily values must fit numeric(28,10) without rounding.");
        }
        if (low <= 0 || open < low || close < low || high < open || high < close || volume < 0)
        {
            throw new ArgumentException("Daily prices must be positive and within low/high; volume must be nonnegative.");
        }
        TradingDate = tradingDate;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
    }

    public DateOnly TradingDate { get; }
    public decimal Open { get; }
    public decimal High { get; }
    public decimal Low { get; }
    public decimal Close { get; }
    public decimal Volume { get; }
}
