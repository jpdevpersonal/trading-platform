using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TradingAssistant.Application;

namespace TradingAssistant.IntegrationTests;

internal sealed class TestApi(string connection = TestApi.Offline, bool injectFault = false,
    Action<IServiceCollection>? configure = null) : WebApplicationFactory<Program>
{
    public const string Offline = "Host=127.0.0.1;Port=1;Database=offline_test;Username=test;Pooling=false";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/TradingAssistant.Api")))
            .UseEnvironment("Development").UseSetting("ConnectionStrings:TradingAssistant", connection)
            .UseSetting("Massive:ApiKey", "")
            .UseSetting("FrontendOrigin", "http://localhost:3000");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(new PriceClock());
            if (injectFault) services.AddScoped<IDatabaseReadiness, Fault>();
            configure?.Invoke(services);
        });
    }

    private sealed class Fault : IDatabaseReadiness
    {
        public Task<bool> CanConnectAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("private-error-sentinel");
    }
}
