using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TradingAssistant.Domain;
using Xunit;

namespace TradingAssistant.IntegrationTests;

public class InstrumentApiTests
{
    private const string FirstId = "10000000-0000-0000-0000-000000000001";
    private const string MissingId = "20000000-0000-0000-0000-000000000001";

    [Fact]
    public async Task StartupAndReadinessDoNotCreateSchemaAndExplicitMigrationIsEmptyAndRepeatable()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        using var api = new TestApi(database.ConnectionString);
        using var client = api.CreateClient();
        const string absentTable = """SELECT to_regclass('"Instruments"') IS NULL""";
        Assert.True(await database.ScalarAsync<bool>(absentTable));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/ready")).StatusCode);
        await AssertProblemAsync(await client.GetAsync("/api/v1/instruments"), HttpStatusCode.InternalServerError);
        Assert.True(await database.ScalarAsync<bool>(absentTable));
        Assert.True(await database.ScalarAsync<bool>("""SELECT to_regclass('"__EFMigrationsHistory"') IS NULL"""));
        await database.MigrateAsync();
        await database.MigrateAsync();
        await using var context = database.CreateContext();
        Assert.Equal(2, (await context.Database.GetAppliedMigrationsAsync()).Count());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        using var page = JsonDocument.Parse(await client.GetStringAsync("/api/v1/instruments"));
        AssertPage(page.RootElement, 0, 50, []);
    }

    [Fact]
    public async Task ListIsOrderedPagedAndDurableAcrossApiHosts()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await database.MigrateAsync();
        await database.ImportAsync();
        var expectedIds = new[] { FirstId, "10000000-0000-0000-0000-000000000002", "10000000-0000-0000-0000-000000000003" };
        for (var restart = 0; restart < 2; restart++)
        {
            using var api = new TestApi(database.ConnectionString);
            using var client = api.CreateClient();
            foreach (var (query, offset, limit, ids) in new[]
            {
                ("", 0, 50, expectedIds),
                ("?offset=1&limit=1", 1, 1, new[] { expectedIds[1] }),
                ("?limit=100", 0, 100, expectedIds),
                ("?offset=2&limit=100", 2, 100, new[] { expectedIds[2] }),
                ("?offset=3", 3, 50, Array.Empty<string>()),
                ("?offset=2147483647", int.MaxValue, 50, Array.Empty<string>())
            })
            {
                using var document = JsonDocument.Parse(await client.GetStringAsync("/api/v1/instruments" + query));
                AssertPage(document.RootElement, offset, limit, ids);
            }
            using var detail = JsonDocument.Parse(await client.GetStringAsync("/api/v1/instruments/" + FirstId));
            AssertInstrument(detail.RootElement, FirstId, "S-AO", "XAAA", "Alder Orbit (sample)", "USD", true);
            await AssertProblemAsync(await client.GetAsync("/api/v1/instruments/" + MissingId), HttpStatusCode.NotFound);
        }
    }

    [Theory]
    [InlineData(0, 1, "AAA")]
    [InlineData(1, 1, "BBB")]
    [InlineData(2, 1, "ZZZ")]
    public async Task DatabaseOrdersBeforeApplyingPagination(int offset, int limit, string expectedSymbol)
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await database.MigrateAsync();
        await using (var context = database.CreateContext())
        {
            context.Instruments.AddRange(
                new Instrument(Guid.NewGuid(), "ZZZ", "XBBB", "Last", "USD", false),
                new Instrument(Guid.NewGuid(), "BBB", "XAAA", "Second", "USD", false),
                new Instrument(Guid.NewGuid(), "AAA", "XAAA", "First", "USD", false));
            await context.SaveChangesAsync();
        }
        using var api = new TestApi(database.ConnectionString);
        using var client = api.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync($"/api/v1/instruments?offset={offset}&limit={limit}"));
        var item = Assert.Single(document.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(expectedSymbol, item.GetProperty("symbol").GetString());
        Assert.False(item.GetProperty("isSample").GetBoolean());
    }

    [Theory]
    [InlineData("?offset=")]
    [InlineData("?limit=")]
    [InlineData("?offset")]
    [InlineData("?limit")]
    [InlineData("?offset=0&offset=1")]
    [InlineData("?limit=1&limit=1")]
    [InlineData("?offset=&offset=0")]
    [InlineData("?limit=&limit=1")]
    [InlineData("?offset=-1")]
    [InlineData("?limit=0")]
    [InlineData("?limit=-1")]
    [InlineData("?limit=101")]
    [InlineData("?offset=2147483648")]
    [InlineData("?limit=2147483648")]
    [InlineData("?offset=-2147483649")]
    [InlineData("?offset=1.0")]
    [InlineData("?limit=abc")]
    [InlineData("?offset=0,1")]
    [InlineData("/not-a-uuid")]
    [InlineData("/00000000-0000-0000-0000-000000000000")]
    [InlineData("/10000000000000000000000000000001")]
    [InlineData("/%7B10000000-0000-0000-0000-000000000001%7D")]
    [InlineData("/%2010000000-0000-0000-0000-000000000001%20")]
    public async Task InvalidInputReturnsProblemDetails(string suffix)
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await database.MigrateAsync();
        using var api = new TestApi(database.ConnectionString);
        using var client = api.CreateClient();
        await AssertProblemAsync(await client.GetAsync("/api/v1/instruments" + suffix), HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/" + FirstId)]
    public async Task DatabaseOutageReturnsSafeProblemDetails(string suffix)
    {
        using var api = new TestApi();
        using var client = api.CreateClient();
        await AssertProblemAsync(await client.GetAsync("/api/v1/instruments" + suffix), HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task DevelopmentOpenApiDescribesInputsPayloadsAndProblems()
    {
        using var api = new TestApi();
        using var client = api.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var root = document.RootElement;
        var paths = root.GetProperty("paths");
        foreach (var (path, errors) in new[]
        {
            ("/api/v1/instruments", new[] { "400", "500" }),
            ("/api/v1/instruments/{id}", new[] { "400", "404", "500" })
        })
        {
            var operation = paths.GetProperty(path).GetProperty("get");
            var responses = operation.GetProperty("responses");
            Assert.True(responses.GetProperty("200").GetProperty("content").TryGetProperty("application/json", out _));
            foreach (var error in errors)
            {
                Assert.True(responses.GetProperty(error).GetProperty("content").TryGetProperty("application/problem+json", out _));
            }
        }
        var parameters = paths.GetProperty("/api/v1/instruments").GetProperty("get").GetProperty("parameters");
        Assert.Equal(2, parameters.GetArrayLength());
        foreach (var (parameter, name, fallback) in new[] { (parameters[0], "offset", 0), (parameters[1], "limit", 50) })
        {
            Assert.Equal(name, parameter.GetProperty("name").GetString());
            Assert.Equal("query", parameter.GetProperty("in").GetString());
            Assert.Equal("integer", parameter.GetProperty("schema").GetProperty("type").GetString());
            Assert.Equal(fallback, parameter.GetProperty("schema").GetProperty("default").GetInt32());
        }
        var id = paths.GetProperty("/api/v1/instruments/{id}").GetProperty("get").GetProperty("parameters")[0];
        Assert.True(id.GetProperty("required").GetBoolean());
        Assert.Equal("uuid", id.GetProperty("schema").GetProperty("format").GetString());
        var schemas = root.GetProperty("components").GetProperty("schemas");
        Assert.Equal(new[] { "currency", "exchangeMic", "id", "isSample", "name", "symbol" },
            schemas.GetProperty("InstrumentResponse").GetProperty("properties").EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal(new[] { "items", "limit", "offset" },
            schemas.GetProperty("InstrumentPageResponse").GetProperty("properties").EnumerateObject().Select(property => property.Name).Order());
    }

    private static void AssertPage(JsonElement page, int offset, int limit, string[] ids)
    {
        Assert.Equal(3, page.EnumerateObject().Count());
        Assert.Equal(offset, page.GetProperty("offset").GetInt32());
        Assert.Equal(limit, page.GetProperty("limit").GetInt32());
        var items = page.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(ids, items.Select(item => item.GetProperty("id").GetString()));
        foreach (var item in items)
        {
            switch (item.GetProperty("id").GetString())
            {
                case FirstId:
                    AssertInstrument(item, FirstId, "S-AO", "XAAA", "Alder Orbit (sample)", "USD", true);
                    break;
                case "10000000-0000-0000-0000-000000000002":
                    AssertInstrument(item, "10000000-0000-0000-0000-000000000002", "S-BG", "XAAA", "Bracken Grid (sample)", "USD", true);
                    break;
                case "10000000-0000-0000-0000-000000000003":
                    AssertInstrument(item, "10000000-0000-0000-0000-000000000003", "S-AO", "XBBB", "Alder Orbit alternate listing (sample)", "EUR", true);
                    break;
            }
        }
    }

    private static void AssertInstrument(JsonElement item, string id, string symbol, string mic, string name, string currency, bool isSample)
    {
        Assert.Equal(6, item.EnumerateObject().Count());
        Assert.Equal(id, item.GetProperty("id").GetString());
        Assert.Equal(symbol, item.GetProperty("symbol").GetString());
        Assert.Equal(mic, item.GetProperty("exchangeMic").GetString());
        Assert.Equal(name, item.GetProperty("name").GetString());
        Assert.Equal(currency, item.GetProperty("currency").GetString());
        Assert.Equal(isSample, item.GetProperty("isSample").GetBoolean());
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        using (response)
        {
            Assert.Equal(status, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            var body = await response.Content.ReadAsStringAsync();
            using var problem = JsonDocument.Parse(body);
            Assert.Equal((int)status, problem.RootElement.GetProperty("status").GetInt32());
            Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("title").GetString()));
            foreach (var sensitive in new[] { "Npgsql", "Exception", "SELECT", "Host=", "Password", "offline_test", "does not exist" })
            {
                Assert.DoesNotContain(sensitive, body, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
