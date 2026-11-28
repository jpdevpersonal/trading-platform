using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TradingAssistant.Application;
using TradingAssistant.Infrastructure;

namespace TradingAssistant.IntegrationTests;

internal sealed class PriceClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 11, 28, 15, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class PriceHandler : HttpMessageHandler
{
    private int calls;
    public int Calls => calls;
    public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? Respond { get; set; }
    public string Payload { get; set; } = Envelope("AAPL", Bar(23), Bar(24));
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref calls);
        return Respond?.Invoke(request, cancellationToken)
            ?? Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(Payload) });
    }

    public static string Bar(int day, decimal close = 11.0000000001m) =>
        JsonSerializer.Serialize(new
        {
            t = new DateTimeOffset(2026, 11, day, 5, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            o = 10.1234567891m,
            h = 12m,
            l = 9m,
            c = close,
            v = 123.0000000001m
        });

    public static string Envelope(string symbol, params string[] bars) =>
        $$"""{"ticker":"{{symbol}}","status":"OK","adjusted":false,"queryCount":{{bars.Length}},"resultsCount":{{bars.Length}},"results":[{{string.Join(",", bars)}}]}""";

    public void Configure(IServiceCollection services, PriceClock clock)
    {
        services.AddSingleton<TimeProvider>(clock);
        services.AddScoped<IDailyPriceProvider>(provider => new MassiveDailyPriceProvider(
            new HttpClient(this, false), provider.GetRequiredService<MassiveRequestBudget>(), clock, "synthetic-key"));
    }
}
