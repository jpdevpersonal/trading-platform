using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TradingAssistant.Application;
using TradingAssistant.Domain;
using Xunit;

namespace TradingAssistant.IntegrationTests;

public class DailyPriceApiTests
{
    internal const string AppleId = "20000000-0000-0000-0000-000000000001";
    internal const string MicrosoftId = "20000000-0000-0000-0000-000000000002";
    internal const string Path = "/api/v1/instruments/" + AppleId + "/daily-prices";
    internal const string Query = "?from=2026-11-23&to=2026-11-25";

    internal static Task SeedAsync(PostgresDatabase database) =>
        database.ExecuteAsync(File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "real-instruments.sql")));

    private static Task<HttpResponseMessage> Import(HttpClient client, string from = "2026-11-23", string to = "2026-11-24", string id = AppleId) =>
        client.PostAsJsonAsync($"/api/v1/instruments/{id}/daily-prices/import", new { from, to });

    private static async Task<PriceImport> Summary(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PriceImport>())!;
    }

    [Fact]
    public async Task ImportRepeatCorrectionEmptyAndSecondStockPreserveExactHistoryAcrossKeylessRestart()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await database.MigrateAsync();
        await SeedAsync(database);
        using var handler = new PriceHandler();
        var clock = new PriceClock();
        using (var api = new TestApi(database.ConnectionString, configure: services => handler.Configure(services, clock)))
        using (var client = api.CreateClient())
        {
            using var initial = JsonDocument.Parse(await client.GetStringAsync(Path + Query));
            var empty = initial.RootElement;
            Assert.True(empty.GetProperty("canImport").GetBoolean());
            Assert.Equal("USD", empty.GetProperty("currency").GetString());
            Assert.Equal(JsonValueKind.Null, empty.GetProperty("source").ValueKind);
            Assert.Equal(JsonValueKind.Null, empty.GetProperty("lastRefreshedAtUtc").ValueKind);
            Assert.Empty(empty.GetProperty("items").EnumerateArray());
            Assert.Equal(0, handler.Calls);
            var first = await Summary(await Import(client));
            Assert.Equal((2, 0, 0), (first.Inserted, first.Updated, first.Unchanged));
            Assert.Equal(clock.Now.UtcDateTime, first.LastRefreshedAtUtc);
            var repeat = await Summary(await Import(client));
            Assert.Equal((0, 0, 2), (repeat.Inserted, repeat.Updated, repeat.Unchanged));
            Assert.True(repeat.LastRefreshedAtUtc > first.LastRefreshedAtUtc);
            handler.Payload = PriceHandler.Envelope("AAPL", PriceHandler.Bar(24, 11.5m), PriceHandler.Bar(25));
            var correction = await Summary(await Import(client, "2026-11-24", "2026-11-25"));
            Assert.Equal((1, 1, 0), (correction.Inserted, correction.Updated, correction.Unchanged));
            handler.Payload = PriceHandler.Envelope("AAPL");
            var weekend = await Summary(await Import(client, "2026-11-21", "2026-11-22"));
            Assert.Equal((0, 0, 0), (weekend.Inserted, weekend.Updated, weekend.Unchanged));
            handler.Payload = PriceHandler.Envelope("MSFT", PriceHandler.Bar(23));
            var other = await Summary(await Import(client, id: MicrosoftId));
            Assert.Equal(1, other.Inserted);
            Assert.Equal(clock.Now.UtcDateTime, other.LastRefreshedAtUtc);
            Assert.True(weekend.LastRefreshedAtUtc > other.LastRefreshedAtUtc);
        }
        handler.Respond = (_, _) => throw new InvalidOperationException("GET must not send HTTP");
        using (var restarted = new TestApi(database.ConnectionString))
        using (var client = restarted.CreateClient())
        {
            var response = await client.GetAsync(Path + Query);
            Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = json.RootElement;
            Assert.Equal("Massive", root.GetProperty("source").GetString());
            Assert.Equal("raw", root.GetProperty("priceBasis").GetString());
            Assert.EndsWith("Z", root.GetProperty("lastRefreshedAtUtc").GetString());
            Assert.Equal("2026-11-21", root.GetProperty("lastRefreshFrom").GetString());
            Assert.Equal("2026-11-22", root.GetProperty("lastRefreshTo").GetString());
            var items = root.GetProperty("items").EnumerateArray().ToArray();
            Assert.Equal(["2026-11-25", "2026-11-24", "2026-11-23"], items.Select(item => item.GetProperty("tradingDate").GetString()));
            Assert.Equal("11.5", items[1].GetProperty("close").GetString());
            Assert.Equal("11.0000000001", items[2].GetProperty("close").GetString());
            Assert.All(items, item =>
            {
                Assert.Equal("10.1234567891", item.GetProperty("open").GetString());
                Assert.Equal("123.0000000001", item.GetProperty("volume").GetString());
            });
            await Problem(await Import(client), HttpStatusCode.ServiceUnavailable);
            using var emptyRange = JsonDocument.Parse(await client.GetStringAsync(Path + "?from=2000-01-01&to=2000-01-02"));
            Assert.Empty(emptyRange.RootElement.GetProperty("items").EnumerateArray());
            Assert.Equal(root.GetProperty("lastRefreshedAtUtc").GetString(), emptyRange.RootElement.GetProperty("lastRefreshedAtUtc").GetString());
            Assert.Equal(root.GetRawText(), await client.GetStringAsync(Path + Query));
        }
        Assert.Equal(5, handler.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentHttpImportsHaveCoherentCountsAndNoDuplicateSessions(bool overlap)
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await database.MigrateAsync();
        await SeedAsync(database);
        using var handler = new PriceHandler();
        var entered = 0;
        var both = new TaskCompletionSource();
        handler.Respond = async (request, token) =>
        {
            if (Interlocked.Increment(ref entered) == 2) both.SetResult();
            await both.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
            var later = request.RequestUri!.AbsolutePath.Contains("/2026-11-24/2026-11-25", StringComparison.Ordinal);
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent(later
                    ? PriceHandler.Envelope("AAPL", PriceHandler.Bar(24), PriceHandler.Bar(25))
                    : PriceHandler.Envelope("AAPL", PriceHandler.Bar(23), PriceHandler.Bar(24)))
            };
        };
        using var api = new TestApi(database.ConnectionString, configure: services => handler.Configure(services, new()));
        using var client1 = api.CreateClient();
        using var client2 = api.CreateClient();
        var responses = await Task.WhenAll(Import(client1), overlap ? Import(client2, "2026-11-24", "2026-11-25") : Import(client2));
        var summaries = await Task.WhenAll(responses.Select(Summary));
        Assert.Equal(overlap ? 3 : 2, summaries.Sum(value => value.Inserted));
        Assert.Equal(overlap ? 1 : 2, summaries.Sum(value => value.Unchanged));
        Assert.Equal(0, summaries.Sum(value => value.Updated));
        await using var context = database.CreateContext();
        Assert.Equal(overlap ? 3 : 2, await context.DailyPrices.CountAsync());
        var latest = summaries.MaxBy(value => value.LastRefreshedAtUtc)!;
        var refresh = await context.DailyPriceRefreshes.SingleAsync();
        Assert.Equal(latest.LastRefreshedAtUtc, refresh.RefreshedAtUtc);
        Assert.Equal((latest.From, latest.To), (refresh.From, refresh.To));
        Assert.Equal(0L, await database.ScalarAsync<long>("""
            SELECT count(*) FROM (SELECT "InstrumentId", "TradingDate" FROM "DailyPrices"
            GROUP BY "InstrumentId", "TradingDate" HAVING count(*) > 1) duplicates
            """));
    }

    [Theory]
    [InlineData("?from=2026-11-23")]
    [InlineData("?from=&to=2026-11-24")]
    [InlineData("?from=2026-11-23&from=2026-11-23&to=2026-11-24")]
    [InlineData("?from=2026-11-23&to=2026-11-24&to=2026-11-24")]
    [InlineData("?from=2026-02-30&to=2026-11-24")]
    [InlineData("?from=2026-1-1&to=2026-11-24")]
    [InlineData("?from=2026-11-25&to=2026-11-24")]
    [InlineData("?from=2025-11-23&to=2026-11-24")]
    public async Task ReadRejectsInvalidOrRepeatedDateQueries(string query)
    {
        using var api = new TestApi();
        using var client = api.CreateClient();
        await Problem(await client.GetAsync(Path + query), HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"from\":\"2026-11-23\"}")]
    [InlineData("{\"from\":\"2026-11-28\",\"to\":\"2026-11-28\"}")]
    [InlineData("{\"from\":\"2024-11-27\",\"to\":\"2024-11-27\"}")]
    [InlineData("{\"from\":\"2025-11-23\",\"to\":\"2026-11-24\"}")]
    [InlineData("{\"from\":\"2026-02-30\",\"to\":\"2026-11-24\"}")]
    [InlineData("{\"from\":42,\"to\":\"2026-11-24\"}")]
    [InlineData("not json")]
    [InlineData("null")]
    public async Task ImportRejectsInvalidJsonAndPlanRangesBeforeAnyProviderCall(string json)
    {
        using var handler = new PriceHandler();
        using var api = new TestApi(configure: services => handler.Configure(services, new()));
        using var client = api.CreateClient();
        await Problem(await client.PostAsync(Path + "/import", new StringContent(json, Encoding.UTF8, "application/json")), HttpStatusCode.BadRequest);
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("not-a-uuid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("20000000000000000000000000000001")]
    public async Task BothRoutesUseCanonicalUuidRules(string id)
    {
        using var api = new TestApi();
        using var client = api.CreateClient();
        await Problem(await Import(client, id: id), HttpStatusCode.BadRequest);
        await Problem(await client.GetAsync($"/api/v1/instruments/{id}/daily-prices" + Query), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CatalogueSelectionRejectsSampleUnmappedOtherMicAndAbsentIdsWithoutHttp()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await database.MigrateAsync();
        await database.ImportAsync();
        await SeedAsync(database);
        var unsupported = new[]
        {
            new Instrument(Guid.NewGuid(), "AAPL", "XNYS", "Different MIC", "USD", false),
            new Instrument(Guid.NewGuid(), "OTHER", "XNAS", "Unmapped", "USD", false)
        };
        await using (var context = database.CreateContext())
        {
            context.Instruments.AddRange(unsupported);
            await context.SaveChangesAsync();
        }
        using var handler = new PriceHandler();
        using var api = new TestApi(database.ConnectionString, configure: services => handler.Configure(services, new()));
        using var client = api.CreateClient();
        for (var offset = 0; offset < 7; offset++)
        {
            using var page = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/instruments?offset={offset}&limit=1"));
            Assert.Single(page.RootElement.GetProperty("items").EnumerateArray());
        }
        foreach (var id in unsupported.Select(row => row.Id.ToString()).Append("10000000-0000-0000-0000-000000000001"))
        {
            await Problem(await Import(client, id: id), HttpStatusCode.UnprocessableEntity);
            using var history = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/instruments/{id}/daily-prices" + Query));
            Assert.False(history.RootElement.GetProperty("canImport").GetBoolean());
        }
        var missing = Guid.NewGuid().ToString();
        await Problem(await Import(client, id: missing), HttpStatusCode.NotFound);
        await Problem(await client.GetAsync($"/api/v1/instruments/{missing}/daily-prices" + Query), HttpStatusCode.NotFound);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task CorsJsonAndOriginGuardsProtectImportWhileLocalClientsAndHealthStillWork()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await database.MigrateAsync();
        await SeedAsync(database);
        using var handler = new PriceHandler();
        using var api = new TestApi(database.ConnectionString, configure: services => handler.Configure(services, new()));
        using var client = api.CreateClient();
        foreach (var origin in new[] { "http://localhost:3000", "http://127.0.0.1:3000" })
        {
            using var preflight = new HttpRequestMessage(HttpMethod.Options, Path + "/import");
            preflight.Headers.Add("Origin", origin);
            preflight.Headers.Add("Access-Control-Request-Method", "POST");
            preflight.Headers.Add("Access-Control-Request-Headers", "content-type");
            var response = await client.SendAsync(preflight);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
            Assert.Contains("POST", string.Join(",", response.Headers.GetValues("Access-Control-Allow-Methods")));
        }
        foreach (var origin in new[] { "https://foreign.invalid", "null", "http://localhost:3000/" })
        {
            foreach (var media in new[] { "application/json", "text/plain", "application/x-www-form-urlencoded" })
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, Path + "/import")
                {
                    Content = new StringContent("""{"from":"2026-11-23","to":"2026-11-24"}""", Encoding.UTF8, media)
                };
                request.Headers.Add("Origin", origin);
                await Problem(await client.SendAsync(request), HttpStatusCode.Forbidden);
            }
        }
        await Problem(await client.PostAsync(Path + "/import", new StringContent("{}")), HttpStatusCode.UnsupportedMediaType);
        Assert.Equal(0, handler.Calls);
        Assert.Equal(2, (await Summary(await Import(client))).Inserted);
        Assert.Equal("{\"status\":\"healthy\"}", await client.GetStringAsync("/api/v1/health"));
        Assert.Equal("{\"status\":\"ready\"}", await client.GetStringAsync("/api/v1/ready"));
    }

    [Fact]
    public async Task ProviderFailuresLeaveSavedRowsAndRefreshUntouchedAndQuotaIsExposed()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await database.MigrateAsync();
        await SeedAsync(database);
        using var handler = new PriceHandler();
        var clock = new PriceClock();
        using var api = new TestApi(database.ConnectionString, configure: services => handler.Configure(services, clock));
        using var client = api.CreateClient();
        await Summary(await Import(client));
        var before = await client.GetStringAsync(Path + Query);
        foreach (var status in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError })
        {
            handler.Status = status;
            await Problem(await Import(client), status == HttpStatusCode.InternalServerError ? HttpStatusCode.BadGateway : HttpStatusCode.ServiceUnavailable);
            Assert.Equal(before, await client.GetStringAsync(Path + Query));
        }
        handler.Status = HttpStatusCode.OK;
        handler.Payload = """{"status":"ERROR","error":"private-upstream-sentinel"}""";
        await Problem(await Import(client), HttpStatusCode.BadGateway);
        var quota = await Import(client);
        await Problem(quota, HttpStatusCode.TooManyRequests);
        Assert.Equal("60", Assert.Single(quota.Headers.GetValues("Retry-After")));
        Assert.Equal(5, handler.Calls);
        clock.Now = clock.Now.AddMinutes(1);
        handler.Respond = (_, _) => throw new TaskCanceledException();
        await Problem(await Import(client), HttpStatusCode.GatewayTimeout);
        Assert.Equal(before, await client.GetStringAsync(Path + Query));
    }

    [Fact]
    public async Task DevelopmentOpenApiDocumentsDailyContractsAndUnexpectedDatabaseErrorsAreSafe()
    {
        using var api = new TestApi();
        using var client = api.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");
        var post = paths.GetProperty("/api/v1/instruments/{id}/daily-prices/import").GetProperty("post");
        Assert.True(post.GetProperty("requestBody").GetProperty("content").TryGetProperty("application/json", out _));
        foreach (var status in new[] { "200", "400", "403", "404", "415", "422", "429", "500", "502", "503", "504" })
            Assert.True(post.GetProperty("responses").TryGetProperty(status, out _));
        var get = paths.GetProperty("/api/v1/instruments/{id}/daily-prices").GetProperty("get");
        Assert.Equal(["id", "from", "to"], get.GetProperty("parameters").EnumerateArray().Select(item => item.GetProperty("name").GetString()));
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        Assert.True(schemas.GetProperty("DailyBarResponse").GetProperty("properties").GetProperty("open").GetProperty("type").GetString() == "string");
        await Problem(await client.GetAsync(Path + Query), HttpStatusCode.InternalServerError);
        await Problem(await Import(client), HttpStatusCode.InternalServerError);
    }

    private static async Task Problem(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        foreach (var secret in new[] { "synthetic-key", "private-upstream-sentinel", "Password", "Npgsql", "SELECT " })
            Assert.DoesNotContain(secret, body, StringComparison.OrdinalIgnoreCase);
    }
}
