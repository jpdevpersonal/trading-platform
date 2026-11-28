using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using TradingAssistant.Application;
using TradingAssistant.Domain;

namespace TradingAssistant.Infrastructure;

public sealed class MassiveRequestBudget(TimeProvider clock)
{
    private readonly object gate = new();
    private readonly Queue<DateTimeOffset> attempts = new();
    private DateTimeOffset blockedUntil;

    public void Acquire(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        lock (gate)
        {
            var now = clock.GetUtcNow();
            while (attempts.TryPeek(out var first) && first <= now.AddMinutes(-1)) attempts.Dequeue();
            var next = attempts.Count >= 5 ? attempts.Peek().AddMinutes(1) : now;
            if (blockedUntil > next) next = blockedUntil;
            if (next > now)
                throw new PriceFailure(PriceFailureKind.Throttled, "Massive request allowance exhausted. Retry after the indicated delay.",
                    Math.Max(1, (int)Math.Ceiling((next - now).TotalSeconds)));
            attempts.Enqueue(now);
        }
    }

    public int Block(TimeSpan? delay)
    {
        var seconds = (int)Math.Clamp(Math.Ceiling((delay ?? TimeSpan.FromMinutes(1)).TotalSeconds), 1, int.MaxValue);
        lock (gate)
        {
            var until = clock.GetUtcNow().AddSeconds(seconds);
            if (until > blockedUntil) blockedUntil = until;
        }
        return seconds;
    }
}

public sealed class MassiveDailyPriceProvider(HttpClient client, MassiveRequestBudget budget, TimeProvider clock, string? apiKey) : IDailyPriceProvider
{
    public bool CanImport(Instrument instrument) => MapTicker(instrument) is not null;

    private static string? MapTicker(Instrument instrument) =>
        (instrument.IsSample, instrument.ExchangeMic, instrument.Symbol, instrument.Currency) switch
        {
            (false, "XNAS", "AAPL", "USD") => "AAPL",
            (false, "XNAS", "MSFT", "USD") => "MSFT",
            _ => null
        };

    public async Task<IReadOnlyList<DailyBar>> FetchAsync(Instrument instrument, PriceRange range, CancellationToken token)
    {
        var ticker = MapTicker(instrument) ?? throw new PriceFailure(PriceFailureKind.Ineligible, "Listing is not supported for import.");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new PriceFailure(PriceFailureKind.Unavailable, "Configure Massive:ApiKey in the backend environment to import daily prices.");
        budget.Acquire(token);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        using var request = new HttpRequestMessage(HttpMethod.Get,
            FormattableString.Invariant($"https://api.massive.com/v2/aggs/ticker/{ticker}/range/1/day/{range.From:yyyy-MM-dd}/{range.To:yyyy-MM-dd}?adjusted=false&sort=asc&limit=50000"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        try
        {
            using var response = await client.SendAsync(request, deadline.Token);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new PriceFailure(PriceFailureKind.Unavailable, "Massive entitlement denied. Check the backend key and Stocks Basic access.");
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var retry = response.Headers.RetryAfter;
                var seconds = budget.Block(retry?.Delta ?? (retry?.Date - clock.GetUtcNow()));
                throw new PriceFailure(PriceFailureKind.Throttled, "Massive is throttling requests. Retry after the indicated delay.", seconds);
            }
            if (!response.IsSuccessStatusCode) throw InvalidResponse();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(deadline.Token));
            var root = document.RootElement;
            var status = root.GetProperty("status").GetString();
            if (status == "NOT_AUTHORIZED")
                throw new PriceFailure(PriceFailureKind.Unavailable, "Massive entitlement denied. Check the backend key and Stocks Basic access.");
            if (status != "OK"
                || root.GetProperty("ticker").GetString() != ticker
                || root.GetProperty("adjusted").GetBoolean()
                || root.TryGetProperty("error", out _)
                || (root.TryGetProperty("next_url", out var next) && next.ValueKind != JsonValueKind.Null))
                throw InvalidResponse();
            var count = root.GetProperty("resultsCount").GetInt32();
            if (count < 0 || count > range.To.DayNumber - range.From.DayNumber + 1
                || root.GetProperty("queryCount").GetInt32() != count)
                throw InvalidResponse();
            var hasResults = root.TryGetProperty("results", out var results);
            if (!hasResults && count == 0) return [];
            if (!hasResults || results.ValueKind != JsonValueKind.Array || results.GetArrayLength() != count)
                throw InvalidResponse();
            var bars = new List<DailyBar>();
            foreach (var result in results.EnumerateArray())
            {
                var date = PriceRange.MarketDate(DateTimeOffset.FromUnixTimeMilliseconds(result.GetProperty("t").GetInt64()));
                bars.Add(new DailyBar(date, Number(result, "o"), Number(result, "h"), Number(result, "l"), Number(result, "c"), Number(result, "v")));
            }
            if (bars.Any(bar => bar.TradingDate < range.From || bar.TradingDate > range.To)
                || bars.Select(bar => bar.TradingDate).Distinct().Count() != count)
                throw InvalidResponse();
            return bars;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new PriceFailure(PriceFailureKind.Timeout, "Massive timed out. Retry the import later.");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or KeyNotFoundException
            or InvalidOperationException or ArgumentException or FormatException or OverflowException)
        {
            throw InvalidResponse();
        }
    }

    private static PriceFailure InvalidResponse() =>
        new(PriceFailureKind.Provider, "Massive returned an invalid or incomplete daily series. Retry later.");

    private static decimal Number(JsonElement result, string name)
    {
        var value = result.GetProperty(name);
        if (value.ValueKind != JsonValueKind.Number) throw InvalidResponse();
        var raw = value.GetRawText();
        var parts = raw.Split(['e', 'E']);
        var exponent = parts.Length == 1 ? 0 : int.Parse(parts[1], CultureInfo.InvariantCulture);
        if (exponent is < -1000 or > 1000) throw InvalidResponse();
        var mantissa = parts[0].TrimStart('-');
        var dot = mantissa.IndexOf('.');
        var scale = (dot < 0 ? 0 : mantissa.Length - dot - 1) - exponent;
        var digits = mantissa.Replace(".", "", StringComparison.Ordinal).TrimStart('0');
        while (digits.EndsWith('0'))
        {
            digits = digits[..^1];
            scale--;
        }
        // Validate the original token before decimal parsing can round excess significant digits.
        if (digits.Length > 0 && (scale > 10 || digits.Length - scale > 18 || digits.Length > 28))
            throw InvalidResponse();
        return decimal.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
