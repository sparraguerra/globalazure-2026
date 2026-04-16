using AgentEvaluator;
using AgentEvaluator.Models;
using Dapr;
using OpenTelemetry.Trace;
using System.Text.Json;

LoadEnvFile();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<EvaluatorAgent>();
builder.Services.AddSingleton<ContentEvaluationService>();
builder.Services.AddSingleton<FoundryEvaluationService>();

builder.AddServiceDefaults();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("evaluator-agent")
        .AddSource("*Microsoft.Extensions.AI")
        .AddSource("*Microsoft.Agents.AI")
        .AddSource("*Microsoft.Extensions.Agents*"));

var app = builder.Build();

app.MapDefaultEndpoints();

// UseCloudEvents must come before route handlers so it can unwrap the
// Dapr CloudEvent envelope before body binding runs.
app.UseCloudEvents();

// A2A Agent Card (serve at both v0.3 and v1.0 well-known paths)
var cardJsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
};
app.MapGet("/.well-known/agent.json", (HttpContext ctx) =>
{
    var scheme = ctx.Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? ctx.Request.Scheme;
    var baseUrl = $"{scheme}://{ctx.Request.Host}";
    return Results.Json(A2ACard.ForBaseUrl(baseUrl), cardJsonOptions);
});
app.MapGet("/.well-known/agent-card.json", (HttpContext ctx) =>
{
    var scheme = ctx.Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? ctx.Request.Scheme;
    var baseUrl = $"{scheme}://{ctx.Request.Host}";
    return Results.Json(A2ACard.ForBaseUrl(baseUrl), cardJsonOptions);
});

// Subscribe to content-created events published by agent-creator.
// MapSubscribeHandler() MUST be registered AFTER all WithTopic() routes
// so it can discover them when Dapr queries GET /_dapr/subscribe on startup.
app.MapPost("/content-created", async (
    ContentCreatedMessage message,
    ContentEvaluationService evaluationService,
    FoundryEvaluationService foundryService,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    logger.LogInformation("[Evaluator] Received content-created event for topic '{Topic}'", message.Topic);
    try
    {
        // 1. LLM-as-judge evaluation (local, fast).
        var localResult = await evaluationService.EvaluateAsync(message, cancellationToken);

        // 2. Foundry cloud evaluation (async, results appear in Foundry portal).
        FoundryEvaluationReport? foundryReport = null;
        if (foundryService.IsAvailable)
        {
            foundryReport = await foundryService.SubmitAsync(message, cancellationToken);
            if (foundryReport is not null)
            {
                logger.LogInformation(
                    "[Foundry] Evaluation run submitted — eval: {EvalId}, run: {RunId}",
                    foundryReport.EvaluationId, foundryReport.EvaluationRunId);
            }
        }

        var result = localResult with { FoundryReport = foundryReport };
        logger.LogInformation(
            "[Evaluator] Complete — overall: {Overall:F1}, foundry run: {RunId}",
            result.OverallScore, foundryReport?.EvaluationRunId ?? "n/a");

        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        // Return 200 to Dapr to prevent infinite delivery retries.
        logger.LogError(ex, "[Evaluator] Unhandled error evaluating topic '{Topic}'", message.Topic);
        return Results.Ok();
    }
}).WithTopic("pubsub", "content-created");

// Must be called AFTER all WithTopic() route registrations.
app.MapSubscribeHandler();

app.MapGet("/health", () => Results.Json(new
{
    status = "healthy",
    agent = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? "evaluator-agent"
}));

app.Run();

/// <summary>
/// Load a .env file into environment variables for local development.
/// Walks up from the current directory looking for a .env file.
/// Does NOT overwrite variables that are already set (e.g. from Docker/CI).
/// </summary>
static void LoadEnvFile()
{
    var dir = Directory.GetCurrentDirectory();
    string? envPath = null;

    for (var d = new DirectoryInfo(dir); d != null; d = d.Parent)
    {
        var candidate = Path.Combine(d.FullName, ".env");
        if (File.Exists(candidate))
        {
            envPath = candidate;
            break;
        }
    }

    if (envPath == null)
    {
        Console.WriteLine("[EvaluatorAgent] No .env file found (walked up from " + dir + ")");
        return;
    }

    Console.WriteLine($"[EvaluatorAgent] Loading env vars from {envPath}");
    foreach (var line in File.ReadAllLines(envPath))
    {
        var trimmed = line.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
            continue;

        var eqIndex = trimmed.IndexOf('=');
        if (eqIndex <= 0) continue;

        var key = trimmed[..eqIndex].Trim();
        var value = trimmed[(eqIndex + 1)..].Trim();

        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            Environment.SetEnvironmentVariable(key, value);
    }
}

// Make the implicit Program class public for test access
public partial class Program { }
