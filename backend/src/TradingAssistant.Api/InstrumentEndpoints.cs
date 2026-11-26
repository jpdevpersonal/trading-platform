using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.OpenApi;
using TradingAssistant.Application;
using TradingAssistant.Domain;

namespace TradingAssistant.Api;

internal static class InstrumentEndpoints
{
    public static void MapInstruments(this RouteGroupBuilder api)
    {
        api.MapGet("/instruments", ListAsync)
            .WithSummary("Read an ordered page of instruments.")
            .WithDescription("Ordered by exchange MIC, then symbol. Omitted offset/limit default to 0/50. Empty, repeated, overflowing or out-of-range values return 400. No total count.")
            .ProducesProblem(400)
            .ProducesProblem(500)
            .AddOpenApiOperationTransformer((operation, _, _) =>
            {
                operation.Parameters =
                [
                    new OpenApiParameter
                    {
                        Name = "offset", In = ParameterLocation.Query,
                        Description = "One nonnegative 32-bit integer; defaults to 0.",
                        Schema = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32", Default = JsonValue.Create(0) }
                    },
                    new OpenApiParameter
                    {
                        Name = "limit", In = ParameterLocation.Query,
                        Description = "One integer from 1 to 100; defaults to 50.",
                        Schema = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32", Default = JsonValue.Create(50) }
                    }
                ];
                return Task.CompletedTask;
            });
        api.MapGet("/instruments/{id}", FindAsync)
            .WithSummary("Read an instrument by its stable UUID.")
            .WithDescription("The id must be a nonempty UUID in standard hyphenated form. Invalid identifiers return 400; missing instruments return 404.")
            .ProducesProblem(400)
            .ProducesProblem(404)
            .ProducesProblem(500)
            .AddOpenApiOperationTransformer((operation, _, _) =>
            {
                operation.Parameters =
                [
                    new OpenApiParameter
                    {
                        Name = "id", In = ParameterLocation.Path, Required = true,
                        Description = "Nonempty UUID in standard hyphenated form.",
                        Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" }
                    }
                ];
                return Task.CompletedTask;
            });
    }

    private static async Task<Results<Ok<InstrumentPageResponse>, ProblemHttpResult>> ListAsync(
        HttpRequest request, InstrumentCatalogue catalogue, CancellationToken cancellationToken)
    {
        if (!TryReadInteger(request.Query, "offset", 0, 0, int.MaxValue, out var offset)
            || !TryReadInteger(request.Query, "limit", 50, 1, 100, out var limit))
        {
            return TypedResults.Problem(statusCode: 400, title: "Invalid pagination.",
                detail: "Supply one nonnegative 32-bit offset and one limit from 1 to 100, or omit them.");
        }

        var instruments = await catalogue.ListAsync(offset, limit, cancellationToken);
        return TypedResults.Ok(new InstrumentPageResponse(instruments.Select(InstrumentResponse.From).ToArray(), offset, limit));
    }

    private static async Task<Results<Ok<InstrumentResponse>, ProblemHttpResult>> FindAsync(
        string id, InstrumentCatalogue catalogue, CancellationToken cancellationToken)
    {
        if (id.Length != 36 || !Guid.TryParseExact(id, "D", out var identifier) || identifier == Guid.Empty)
        {
            return TypedResults.Problem(statusCode: 400, title: "Invalid instrument identifier.",
                detail: "Supply a nonempty UUID in standard hyphenated form.");
        }

        var instrument = await catalogue.FindAsync(identifier, cancellationToken);
        return instrument is null
            ? TypedResults.Problem(statusCode: 404, title: "Instrument not found.")
            : TypedResults.Ok(InstrumentResponse.From(instrument));
    }

    private static bool TryReadInteger(IQueryCollection query, string name, int fallback, int minimum, int maximum, out int value)
    {
        value = fallback;
        return !query.TryGetValue(name, out var values)
            || (values.Count == 1
                && int.TryParse(values[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value)
                && value >= minimum && value <= maximum);
    }
}

internal sealed record InstrumentPageResponse(IReadOnlyList<InstrumentResponse> Items, int Offset, int Limit);

internal sealed record InstrumentResponse(Guid Id, string Symbol, string ExchangeMic, string Name, string Currency, bool IsSample)
{
    public static InstrumentResponse From(Instrument instrument) =>
        new(instrument.Id, instrument.Symbol, instrument.ExchangeMic, instrument.Name, instrument.Currency, instrument.IsSample);
}
