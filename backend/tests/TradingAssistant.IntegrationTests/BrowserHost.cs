using System.Net;
using System.Runtime.InteropServices;
using TradingAssistant.Domain;

namespace TradingAssistant.IntegrationTests;

internal static class BrowserHost
{
    public static async Task Main(string[] args)
    {
        if (args is not ["--browser-host"])
            throw new ArgumentException("This test-only executable requires --browser-host and TRADING_TEST_CONNECTION.");
        await using var database = await PostgresDatabase.CreateAsync();
        await database.MigrateAsync();
        await DailyPriceApiTests.SeedAsync(database);
        await using (var context = database.CreateContext())
        {
            for (var index = 0; index < 50; index++)
                context.Instruments.Add(new Instrument(Guid.NewGuid(), $"S-{index:D2}", "XAAA", "Synthetic sample", "USD", true));
            await context.SaveChangesAsync();
        }
        using var handler = new PriceHandler();
        handler.Respond = (request, _) => Task.FromResult(new HttpResponseMessage(handler.Calls == 3 ? HttpStatusCode.BadGateway : HttpStatusCode.OK)
        {
            Content = new StringContent(PriceHandler.Envelope(
                request.RequestUri!.AbsolutePath.Contains("/MSFT/", StringComparison.Ordinal) ? "MSFT" : "AAPL",
                PriceHandler.Bar(23), PriceHandler.Bar(24)))
        });
        using var api = new TestApi(database.ConnectionString, configure: services => handler.Configure(services, new()));
        api.UseKestrel(options => options.ListenLocalhost(5080));
        api.StartServer();
        using var shutdown = new CancellationTokenSource();
        using var term = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context => { context.Cancel = true; shutdown.Cancel(); });
        Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; shutdown.Cancel(); };
        Console.WriteLine("Synthetic upstream browser host ready on loopback; real API and isolated PostgreSQL.");
        try { await Task.Delay(Timeout.Infinite, shutdown.Token); }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
    }
}
