using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TradingAssistant.Application;
using TradingAssistant.Infrastructure;
using Xunit;

namespace TradingAssistant.IntegrationTests;

public class ApiTests
{
    private const string Offline = "Host=127.0.0.1;Port=1;Database=offline_test;Username=test;Pooling=false";
    private const string Origin = "http://localhost:3000";

    private sealed class Api(string connection = Offline, bool injectFault = false) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development").UseSetting("ConnectionStrings:TradingAssistant", connection)
                .UseSetting("FrontendOrigin", Origin);
            if (injectFault)
            {
                builder.ConfigureTestServices(services => services.AddScoped<IDatabaseReadiness, Fault>());
            }
        }
    }

    private sealed class Fault : IDatabaseReadiness
    {
        public Task<bool> CanConnectAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("private-error-sentinel");
    }

    [Fact]
    public async Task RealPostgresIsReady()
    {
        var connection = Environment.GetEnvironmentVariable("TRADING_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(connection), "Set TRADING_TEST_CONNECTION to an isolated PostgreSQL database (see README).");
        var settings = new NpgsqlConnectionStringBuilder(connection);
        Assert.True(settings.Host is "127.0.0.1" or "localhost" && settings.Database?.EndsWith("_test", StringComparison.Ordinal) == true,
            "Tests require a loopback PostgreSQL database ending in _test.");
        using var api = new Api(connection);
        using var client = api.CreateClient();
        using var scope = api.Services.CreateScope();
        Assert.True(scope.ServiceProvider.GetRequiredService<TradingContext>().Database.GetConnectionString() == connection,
            "Refusing to probe a connection other than the supplied test database.");
        using var response = await client.GetAsync("/api/v1/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("""{"status":"ready"}""", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task LivenessAndOpenApiSurviveDatabaseOutage()
    {
        using var api = new Api();
        using var client = api.CreateClient();
        using var ready = await client.GetAsync("/api/v1/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal("""{"status":"unavailable"}""", await ready.Content.ReadAsStringAsync());
        using var health = await client.GetAsync("/api/v1/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal("application/json", health.Content.Headers.ContentType?.MediaType);
        Assert.Equal("""{"status":"healthy"}""", await health.Content.ReadAsStringAsync());
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        Assert.True(document.RootElement.GetProperty("paths").GetProperty("/api/v1/health").TryGetProperty("get", out _));
    }

    [Theory]
    [InlineData(Origin, true)]
    [InlineData("http://localhost:3999", false)]
    public async Task CorsRestrictsRequestsAndPreflights(string origin, bool allowed)
    {
        using var api = new Api();
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", origin);
        using var health = await client.GetAsync("/api/v1/health");
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/health");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "accept");
        using var preflight = await client.SendAsync(request);
        foreach (var response in new[] { health, preflight })
        {
            Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Origin"));
            if (allowed)
            {
                Assert.Equal(Origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
            }
        }
        if (allowed)
        {
            Assert.Equal("GET", Assert.Single(preflight.Headers.GetValues("Access-Control-Allow-Methods")));
            Assert.Equal("Accept", Assert.Single(preflight.Headers.GetValues("Access-Control-Allow-Headers")), ignoreCase: true);
        }
    }

    [Fact]
    public async Task UnexpectedFailureReturnsSafeProblemEvenInDevelopment()
    {
        using var api = new Api(injectFault: true);
        using var client = api.CreateClient();
        using var response = await client.GetAsync("/api/v1/ready");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        using var problem = JsonDocument.Parse(body);
        Assert.Equal(500, problem.RootElement.GetProperty("status").GetInt32());
        Assert.DoesNotContain("private-error-sentinel", body);
        Assert.DoesNotContain("InvalidOperationException", body);
    }
}
