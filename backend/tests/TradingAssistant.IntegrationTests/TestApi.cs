using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TradingAssistant.Application;

namespace TradingAssistant.IntegrationTests;

internal sealed class TestApi(string connection = TestApi.Offline, bool injectFault = false) : WebApplicationFactory<Program>
{
    public const string Offline = "Host=127.0.0.1;Port=1;Database=offline_test;Username=test;Pooling=false";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development").UseSetting("ConnectionStrings:TradingAssistant", connection)
            .UseSetting("FrontendOrigin", "http://localhost:3000");
        if (injectFault)
        {
            builder.ConfigureTestServices(services => services.AddScoped<IDatabaseReadiness, Fault>());
        }
    }

    private sealed class Fault : IDatabaseReadiness
    {
        public Task<bool> CanConnectAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("private-error-sentinel");
    }
}
