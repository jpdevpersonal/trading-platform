using System.Globalization;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.OpenApi;
using TradingAssistant.Application;

namespace TradingAssistant.Api;

internal sealed record BrowserOrigins(string[] Values);
internal sealed record PriceImportRequest([property: Required] string From, [property: Required] string To);
internal sealed record DailyBarResponse(string TradingDate, string Open, string High, string Low, string Close, string Volume);
internal sealed record DailyHistoryResponse(Guid InstrumentId, string Currency, bool CanImport, IReadOnlyList<DailyBarResponse> Items,
    string? Source, string? PriceBasis, DateTime? LastRefreshedAtUtc, DateOnly? LastRefreshFrom, DateOnly? LastRefreshTo);

internal static class DailyPriceEndpoints
{
    public static void MapDailyPrices(this RouteGroupBuilder api)
    {
        var read = api.MapGet("/instruments/{id}/daily-prices", ReadAsync)
            .WithSummary("Read saved unadjusted daily prices, newest first; never contacts Massive.")
            .Produces<DailyHistoryResponse>();
        var import = api.MapPost("/instruments/{id}/daily-prices/import", ImportAsync)
            .WithSummary("Manually import a completed US daily range from Massive.")
            .WithDescription("Maximum 366 calendar days inclusive. Start within two calendar years; end yesterday or earlier in America/New_York. JSON only; explicit browser Origin must be allowlisted.")
            .Produces<PriceImport>()
            .ProducesProblem(403).ProducesProblem(415).ProducesProblem(422).ProducesProblem(429)
            .ProducesProblem(502).ProducesProblem(503).ProducesProblem(504);
        import.AddOpenApiOperationTransformer(async (operation, context, token) =>
        {
            operation.RequestBody = new OpenApiRequestBody
            {
                Required = true,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new() { Schema = await context.GetOrCreateSchemaAsync(typeof(PriceImportRequest), cancellationToken: token) }
                }
            };
        });
        foreach (var route in new[] { read, import })
        {
            route.RequireCors("DailyPrices").ProducesProblem(400).ProducesProblem(404).ProducesProblem(500)
                .AddOpenApiOperationTransformer((operation, _, _) =>
                {
                    operation.Parameters =
                    [
                        new OpenApiParameter
                        {
                            Name = "id", In = ParameterLocation.Path, Required = true,
                            Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" }
                        }
                    ];
                    return Task.CompletedTask;
                });
        }
        read.AddOpenApiOperationTransformer((operation, _, _) =>
        {
            foreach (var name in new[] { "from", "to" })
                operation.Parameters!.Add(new OpenApiParameter
                {
                    Name = name,
                    In = ParameterLocation.Query,
                    Required = true,
                    Description = "One exact YYYY-MM-DD date; inclusive range is at most 366 calendar days.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "date" }
                });
            return Task.CompletedTask;
        });
    }

    private static async Task<IResult> ReadAsync(string id, HttpContext http, DailyPrices prices, ILoggerFactory logs, CancellationToken token)
    {
        http.Response.Headers.CacheControl = "no-store";
        if (!TryId(id, out var identifier)) return InvalidId();
        if (!Single(http.Request.Query, "from", out var from) || !Single(http.Request.Query, "to", out var to)
            || !TryRange(from, to, out var range)) return InvalidDates();
        try
        {
            var history = await prices.ReadAsync(identifier, range!, token);
            var refresh = history.Stored.Refresh;
            return Results.Ok(new DailyHistoryResponse(identifier, history.Currency, history.CanImport,
                history.Stored.Items.Select(bar => new DailyBarResponse(bar.TradingDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Decimal(bar.Open), Decimal(bar.High), Decimal(bar.Low), Decimal(bar.Close), Decimal(bar.Volume))).ToArray(),
                refresh?.Source, refresh?.PriceBasis, refresh?.RefreshedAtUtc, refresh?.From, refresh?.To));
        }
        catch (PriceFailure failure) { return Problem(failure, http, logs); }
    }

    private static async Task<IResult> ImportAsync(string id, HttpContext http, DailyPrices prices, BrowserOrigins origins,
        ILoggerFactory logs, CancellationToken token)
    {
        http.Response.Headers.CacheControl = "no-store";
        var suppliedOrigin = http.Request.Headers.Origin;
        if (suppliedOrigin.Count > 0 && (suppliedOrigin.Count != 1 || !origins.Values.Contains(suppliedOrigin[0], StringComparer.Ordinal)))
            return Results.Problem(statusCode: 403, title: "Browser origin is not allowed.");
        var contentType = http.Request.ContentType?.Split(';')[0].Trim();
        if (!string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase))
            return Results.Problem(statusCode: 415, title: "Use application/json for daily imports.");
        if (!TryId(id, out var identifier)) return InvalidId();
        PriceImportRequest? body;
        try { body = await http.Request.ReadFromJsonAsync<PriceImportRequest>(token); }
        catch (JsonException) { return InvalidDates(); }
        catch (BadHttpRequestException) { return InvalidDates(); }
        if (!TryRange(body?.From, body?.To, out var range)) return InvalidDates();
        try { return Results.Ok(await prices.ImportAsync(identifier, range!, token)); }
        catch (PriceFailure failure) { return Problem(failure, http, logs); }
    }

    private static bool Single(IQueryCollection query, string name, out string? value)
    {
        value = null;
        if (!query.TryGetValue(name, out var values) || values.Count != 1) return false;
        value = values[0];
        return true;
    }

    private static bool TryRange(string? from, string? to, out PriceRange? range)
    {
        range = null;
        if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
            || !DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
            return false;
        range = new(start, end);
        return true;
    }

    private static bool TryId(string id, out Guid value)
    {
        value = Guid.Empty;
        return id.Length == 36 && Guid.TryParseExact(id, "D", out value) && value != Guid.Empty;
    }

    private static IResult InvalidId() => Results.Problem(statusCode: 400, title: "Invalid instrument identifier.",
        detail: "Supply a nonempty UUID in standard hyphenated form.");
    private static IResult InvalidDates() => Results.Problem(statusCode: 400, title: "Invalid date range.",
        detail: "Supply exactly one from and to, each an exact YYYY-MM-DD calendar date.");
    private static string Decimal(decimal value) => value.ToString("0.##########", CultureInfo.InvariantCulture);

    private static IResult Problem(PriceFailure failure, HttpContext http, ILoggerFactory logs)
    {
        var status = failure.Kind switch
        {
            PriceFailureKind.InvalidRange => 400,
            PriceFailureKind.NotFound => 404,
            PriceFailureKind.Ineligible => 422,
            PriceFailureKind.Throttled => 429,
            PriceFailureKind.Unavailable => 503,
            PriceFailureKind.Timeout => 504,
            _ => 502
        };
        logs.CreateLogger("DailyPrices").LogWarning("Daily price request failed: {FailureKind}", failure.Kind);
        if (failure.RetryAfterSeconds is { } seconds)
            http.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        return Results.Problem(statusCode: status, title: failure.Message);
    }
}
