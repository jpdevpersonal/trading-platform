using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using TradingAssistant.Api;
using TradingAssistant.Application;
using TradingAssistant.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration["urls"] ??= "http://localhost:5080";
var origin = LocalOrigin.Parse(builder.Configuration["FrontendOrigin"]);
var additionalOrigins = builder.Configuration.GetSection("AdditionalFrontendOrigins").Get<string[]>() ?? [];
var origins = new[] { origin }.Concat(additionalOrigins.Select(LocalOrigin.Parse)).ToArray();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(origins).WithMethods("GET").WithHeaders("Accept")));
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddScoped<InstrumentCatalogue>();
builder.Services.AddPersistence(builder.Configuration.GetConnectionString("TradingAssistant"));

var app = builder.Build();
app.Use(async (context, next) =>
{
    var start = Stopwatch.GetTimestamp();
    await next(context);
    app.Logger.LogInformation("HTTP {Method} {Route} returned {StatusCode} in {ElapsedMs} ms",
        context.Request.Method, (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "(unmatched)",
        context.Response.StatusCode, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
});
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    SuppressDiagnosticsCallback = _ => true,
    ExceptionHandler = context => Results.Problem(statusCode: 500, title: "An unexpected error occurred.")
        .ExecuteAsync(context)
});
app.UseCors();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var api = app.MapGroup("/api/v1");
api.MapInstruments();
api.MapGet("/health", () => new StatusResponse("healthy"));
api.MapGet("/ready", async (IDatabaseReadiness database, CancellationToken token) =>
    await database.CanConnectAsync(token)
        ? Results.Ok(new StatusResponse("ready"))
        : Results.Json(new StatusResponse("unavailable"), statusCode: StatusCodes.Status503ServiceUnavailable))
    .Produces<StatusResponse>().Produces<StatusResponse>(503);

app.Run();

internal sealed record StatusResponse(string Status);
public partial class Program;
