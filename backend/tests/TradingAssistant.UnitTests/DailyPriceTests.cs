using System.Globalization;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using TradingAssistant.Application;
using TradingAssistant.Domain;
using TradingAssistant.Infrastructure;
using Xunit;

namespace TradingAssistant.UnitTests;

public class DailyPriceTests
{
    private static readonly Instrument Apple = new(Guid.NewGuid(), "AAPL", "XNAS", "Apple", "USD", false);
    private static readonly PriceRange Range = new(new(2026, 3, 6), new(2026, 3, 10));
    private const string Bar = """{"t":1772773200000,"o":10.1234567891,"h":12,"l":9,"c":11,"v":123.0000000001}""";
    private static string Envelope(string bars = Bar, int count = 1) =>
        $$"""{"status":"OK","ticker":"AAPL","adjusted":false,"queryCount":{{count}},"resultsCount":{{count}},"results":[{{bars}}]}""";

    [Fact]
    public async Task OutboundRequestUsesFixedHostMappedTickerRawDailyRangeAndBearerOnly()
    {
        var clock = new Clock();
        foreach (var symbol in new[] { "AAPL", "MSFT" })
        {
            using var handler = new Handler((request, _) =>
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Equal($"https://api.massive.com/v2/aggs/ticker/{symbol}/range/1/day/2026-03-06/2026-03-10?adjusted=false&sort=asc&limit=50000", request.RequestUri!.AbsoluteUri);
                Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
                Assert.Equal("synthetic-key", request.Headers.Authorization.Parameter);
                return Task.FromResult(Json(Envelope().Replace("AAPL", symbol, StringComparison.Ordinal)));
            });
            using var client = new HttpClient(handler);
            var provider = new MassiveDailyPriceProvider(client, new(clock), clock, "synthetic-key");
            var bars = await provider.FetchAsync(new(Guid.NewGuid(), symbol, "XNAS", symbol, "USD", false), Range, default);
            Assert.Equal(10.1234567891m, Assert.Single(bars).Open);
            Assert.Equal(123.0000000001m, bars[0].Volume);
            Assert.Equal(new DateOnly(2026, 3, 6), bars[0].TradingDate);
        }
    }

    [Theory]
    [InlineData("AAPL", "XNAS", "USD", true)]
    [InlineData("AAPL", "XNYS", "USD", false)]
    [InlineData("AAPL", "XNAS", "GBP", false)]
    [InlineData("OTHER", "XNAS", "USD", false)]
    public async Task IneligibleListingsNeverReachHttp(string symbol, string mic, string currency, bool sample)
    {
        using var handler = new Handler((_, _) => throw new InvalidOperationException("Must not call"));
        using var client = new HttpClient(handler);
        var clock = new Clock();
        var provider = new MassiveDailyPriceProvider(client, new(clock), clock, "synthetic-key");
        var instrument = new Instrument(Guid.NewGuid(), symbol, mic, "Other", currency, sample);
        Assert.False(provider.CanImport(instrument));
        Assert.Equal(PriceFailureKind.Ineligible, (await Assert.ThrowsAsync<PriceFailure>(() => provider.FetchAsync(instrument, Range, default))).Kind);
        Assert.Equal(0, handler.Calls);
    }

    public static IEnumerable<object[]> BadPayloads()
    {
        yield return ["not json"];
        yield return ["null"];
        foreach (var (oldValue, newValue) in new[]
        {
            ("\"OK\"", "\"ERROR\""), ("\"AAPL\"", "\"MSFT\""), ("\"adjusted\":false", "\"adjusted\":true"),
            ("\"resultsCount\":1", "\"resultsCount\":2"), ("\"queryCount\":1", "\"queryCount\":50000"),
            ("\"o\":10.1234567891", "\"o\":0"), ("\"v\":123.0000000001", "\"v\":-1"),
            ("\"h\":12", "\"h\":10"), ("\"l\":9", "\"l\":11"),
            ("\"o\":10.1234567891", "\"o\":10.12345678910000000000000000001"),
            ("\"v\":123.0000000001", "\"v\":1000000000000000000"),
            ("\"v\":123.0000000001", "\"v\":1e-11"),
            ("1772773200000", "1772686800000")
        }) yield return [Envelope().Replace(oldValue, newValue, StringComparison.Ordinal)];
        yield return [Envelope() + "trailing"];
        yield return [Envelope().Replace("\"status\":", "\"next_url\":\"https://untrusted.invalid/secret\",\"status\":", StringComparison.Ordinal)];
        yield return [Envelope().Replace("\"status\":", "\"error\":\"private provider text\",\"status\":", StringComparison.Ordinal)];
        yield return [Envelope(Bar + "," + Bar, 2)];
    }

    [Theory]
    [MemberData(nameof(BadPayloads))]
    public async Task MalformedIncompleteOrInvalidBarsFailSafely(string payload)
    {
        using var handler = new Handler((_, _) => Task.FromResult(Json(payload)));
        using var client = new HttpClient(handler);
        var clock = new Clock();
        var provider = new MassiveDailyPriceProvider(client, new(clock), clock, "synthetic-key");
        var failure = await Assert.ThrowsAsync<PriceFailure>(() => provider.FetchAsync(Apple, Range, default));
        Assert.Equal(PriceFailureKind.Provider, failure.Kind);
        Assert.DoesNotContain("private", failure.ToString());
        Assert.DoesNotContain("synthetic-key", failure.ToString());
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SuccessfulEmptyEnvelopeDoesNotManufactureSessions(bool includeResults)
    {
        var payload = Envelope("", 0);
        if (!includeResults) payload = payload.Replace(",\"results\":[]", "", StringComparison.Ordinal);
        using var handler = new Handler((_, _) => Task.FromResult(Json(payload)));
        using var client = new HttpClient(handler);
        var clock = new Clock();
        Assert.Empty(await new MassiveDailyPriceProvider(client, new(clock), clock, "synthetic-key").FetchAsync(Apple, Range, default));
    }

    [Fact]
    public async Task HttpSuccessWithDeniedEntitlementStillReturnsSetupFailure()
    {
        using var handler = new Handler((_, _) => Task.FromResult(Json("""{"status":"NOT_AUTHORIZED","message":"private entitlement detail"}""")));
        using var client = new HttpClient(handler);
        var clock = new Clock();
        var failure = await Assert.ThrowsAsync<PriceFailure>(() =>
            new MassiveDailyPriceProvider(client, new(clock), clock, "synthetic-key").FetchAsync(Apple, Range, default));
        Assert.Equal(PriceFailureKind.Unavailable, failure.Kind);
        Assert.DoesNotContain("private", failure.ToString());
    }

    [Theory]
    [InlineData(401, PriceFailureKind.Unavailable)]
    [InlineData(403, PriceFailureKind.Unavailable)]
    [InlineData(429, PriceFailureKind.Throttled)]
    [InlineData(500, PriceFailureKind.Provider)]
    [InlineData(302, PriceFailureKind.Provider)]
    public async Task HttpFailuresHaveSafeGuidanceAndNoRetries(int status, PriceFailureKind expected)
    {
        using var handler = new Handler((_, _) =>
        {
            var response = new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent("private upstream body") };
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(90));
            return Task.FromResult(response);
        });
        using var client = new HttpClient(handler);
        var clock = new Clock();
        var budget = new MassiveRequestBudget(clock);
        var provider = new MassiveDailyPriceProvider(client, budget, clock, "synthetic-key");
        var failure = await Assert.ThrowsAsync<PriceFailure>(() => provider.FetchAsync(Apple, Range, default));
        Assert.Equal(expected, failure.Kind);
        Assert.DoesNotContain("private", failure.ToString());
        Assert.Equal(1, handler.Calls);
        if (status == 429)
        {
            Assert.Equal(90, failure.RetryAfterSeconds);
            clock.Now = clock.Now.AddSeconds(60);
            Assert.Equal(30, Assert.Throws<PriceFailure>(() => budget.Acquire(default)).RetryAfterSeconds);
        }
    }

    [Fact]
    public async Task MissingKeyConsumesNoRequestAndTimeoutIsDistinctFromCancellation()
    {
        using var handler = new Handler((_, _) => throw new TaskCanceledException());
        using var client = new HttpClient(handler);
        var clock = new Clock();
        var budget = new MassiveRequestBudget(clock);
        Assert.Equal(PriceFailureKind.Unavailable, (await Assert.ThrowsAsync<PriceFailure>(() =>
            new MassiveDailyPriceProvider(client, budget, clock, null).FetchAsync(Apple, Range, default))).Kind);
        Assert.Equal(0, handler.Calls);
        var provider = new MassiveDailyPriceProvider(client, budget, clock, "synthetic-key");
        Assert.Equal(PriceFailureKind.Timeout, (await Assert.ThrowsAsync<PriceFailure>(() => provider.FetchAsync(Apple, Range, default))).Kind);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.FetchAsync(Apple, Range, cancelled.Token));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task CancellationInterruptsAnInFlightHttpRequest()
    {
        var entered = new TaskCompletionSource();
        using var handler = new Handler(async (_, token) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return Json(Envelope());
        });
        using var client = new HttpClient(handler);
        using var cancel = new CancellationTokenSource();
        var clock = new Clock();
        var task = new MassiveDailyPriceProvider(client, new(clock), clock, "synthetic-key").FetchAsync(Apple, Range, cancel.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task AdapterEnforcesItsTenSecondDeadlineWithoutHttpClientTimeout()
    {
        using var handler = new Handler(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return Json(Envelope());
        });
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var clock = new Clock();
        var elapsed = Stopwatch.StartNew();
        var failure = await Assert.ThrowsAsync<PriceFailure>(() =>
            new MassiveDailyPriceProvider(client, new(clock), clock, "synthetic-key").FetchAsync(Apple, Range, default)
                .WaitAsync(TimeSpan.FromSeconds(15)));
        Assert.Equal(PriceFailureKind.Timeout, failure.Kind);
        Assert.InRange(elapsed.Elapsed.TotalSeconds, 9, 15);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task SharedRollingBudgetCountsFailedAttemptsAcrossClientsAndConcurrentCalls()
    {
        var clock = new Clock();
        var budget = new MassiveRequestBudget(clock);
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)));
        using var client = new HttpClient(handler);
        var kinds = await Task.WhenAll(Enumerable.Range(0, 12).Select(async _ =>
            (await Assert.ThrowsAsync<PriceFailure>(() =>
                new MassiveDailyPriceProvider(client, budget, clock, "synthetic-key").FetchAsync(Apple, Range, default))).Kind));
        Assert.Equal(5, kinds.Count(kind => kind == PriceFailureKind.Provider));
        Assert.Equal(7, kinds.Count(kind => kind == PriceFailureKind.Throttled));
        Assert.Equal(5, handler.Calls);
        clock.Now = clock.Now.AddSeconds(59);
        Assert.Equal(1, Assert.Throws<PriceFailure>(() => budget.Acquire(default)).RetryAfterSeconds);
        clock.Now = clock.Now.AddSeconds(1);
        budget.Acquire(default);
    }

    [Theory]
    [InlineData("2026-03-08T04:59:59Z", "2026-03-07")]
    [InlineData("2026-03-08T05:00:00Z", "2026-03-08")]
    [InlineData("2026-03-09T04:00:00Z", "2026-03-09")]
    [InlineData("2026-11-01T04:00:00Z", "2026-11-01")]
    [InlineData("2026-11-02T05:00:00Z", "2026-11-02")]
    public void SessionDatesRespectNewYorkDaylightSaving(string instant, string expected) =>
        Assert.Equal(DateOnly.Parse(expected, CultureInfo.InvariantCulture), PriceRange.MarketDate(DateTimeOffset.Parse(instant, CultureInfo.InvariantCulture)));

    [Fact]
    public void ImportAndReadRangesHaveDistinctCalendarBounds()
    {
        var clock = new Clock { Now = new DateTimeOffset(2026, 3, 10, 2, 0, 0, TimeSpan.Zero) };
        new PriceRange(new(2024, 3, 9), new(2024, 3, 9)).Validate(true, clock);
        new PriceRange(new(2025, 3, 8), new(2026, 3, 8)).Validate(true, clock);
        new PriceRange(new(2000, 1, 1), new(2000, 1, 1)).Validate(false, clock);
        foreach (var range in new[]
        {
            new PriceRange(new(2026, 3, 9), new(2026, 3, 9)),
            new PriceRange(new(2024, 3, 8), new(2024, 3, 8)),
            new PriceRange(new(2025, 3, 7), new(2026, 3, 8)),
            new PriceRange(new(2026, 3, 8), new(2026, 3, 7))
        }) Assert.Throws<PriceFailure>(() => range.Validate(true, clock));
    }

    [Theory]
    [InlineData("0", "1", "0", "1", "0")]
    [InlineData("2", "1", "1", "1", "0")]
    [InlineData("1", "2", "1", "3", "0")]
    [InlineData("1", "2", "1", "1", "-1")]
    [InlineData("1", "2", "1", "1", "0.00000000001")]
    [InlineData("1", "2", "1", "1", "1000000000000000000")]
    public void DomainRejectsInvalidOhlcv(string open, string high, string low, string close, string volume) =>
        Assert.Throws<ArgumentException>(() => new DailyBar(new(2026, 1, 1), Parse(open), Parse(high), Parse(low), Parse(close), Parse(volume)));

    private static decimal Parse(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);
    private static HttpResponseMessage Json(string payload) => new(HttpStatusCode.OK) { Content = new StringContent(payload) };

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 3, 11, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        private int calls;
        public int Calls => calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            return send(request, cancellationToken);
        }
    }
}
