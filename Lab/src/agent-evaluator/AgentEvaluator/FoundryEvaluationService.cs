#pragma warning disable OPENAI001  // EvaluationClient is experimental but stable enough for lab use

using AgentEvaluator.Models;
using Azure.AI.OpenAI;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using OpenAI.Chat;
using OpenAI.Evals;
using System.ClientModel;
using System.Text.Json;

namespace AgentEvaluator;

/// <summary>
/// Submits generated content to Azure AI Foundry as a cloud evaluation run using
/// the OpenAI Evals API exposed by AzureOpenAIClient.
///
/// Flow per received content package:
///   1. Create an evaluation definition once (cached) with builtin.coherence + builtin.fluency.
///   2. Create an evaluation run with inline items (blog post + LinkedIn post).
///   3. Return a <see cref="FoundryEvaluationReport"/> — results appear in the Foundry portal.
///
/// Required env vars (same as the LLM-as-judge path):
///   AZURE_OPENAI_ENDPOINT  — Azure OpenAI or Foundry project endpoint
///   AZURE_OPENAI_API_KEY   — optional; DefaultAzureCredential is used when absent
///   AZURE_OPENAI_DEPLOYMENT — model deployment name used as LLM judge
/// </summary>
public class FoundryEvaluationService
{
    private const string EvalDefinitionName = "content-creator-quality-evaluation";
    private static readonly string[] BuiltInEvaluators = ["score_model:coherence", "score_model:fluency"];

    private readonly EvaluationClient? _evalClient;
    private readonly string _modelDeployment;
    private readonly ILogger<FoundryEvaluationService> _logger;

    private string? _evaluationId;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public FoundryEvaluationService(ILogger<FoundryEvaluationService> logger)
    {
        _logger = logger;
        _modelDeployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT")
               ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
               ?? "gpt-4o";

        var foundryEndpoint = Environment.GetEnvironmentVariable("AZURE_AI_PROJECT_ENDPOINT");
        var aoaiEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        var aoaiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");

        try
        {
            if (!string.IsNullOrEmpty(foundryEndpoint))
            {
                // Use AIProjectClient with DefaultAzureCredential (Entra ID)
                var projectClientOptions = new AIProjectClientOptions(AIProjectClientOptions.ServiceVersion.V2025_05_01);
                var projectClient = new AIProjectClient(new Uri(foundryEndpoint), new DefaultAzureCredential(), projectClientOptions);
                _evalClient = projectClient.GetProjectOpenAIClient().GetEvaluationClient();

                _logger.LogInformation("[Foundry] EvaluationClient initialized for {Endpoint}", foundryEndpoint);
            }
            else if (!string.IsNullOrEmpty(aoaiEndpoint) && !string.IsNullOrEmpty(aoaiKey))
            {
                // Pin to 2025-04-01-preview: the first API version that exposes
                // POST /openai/evals/{id}/runs. Earlier default versions expose eval
                // definitions but return 404 on the runs sub-path.
                var clientOptions = new AzureOpenAIClientOptions(
                    AzureOpenAIClientOptions.ServiceVersion.V2025_04_01_Preview);
                AzureOpenAIClient aoaiClient = string.IsNullOrEmpty(aoaiKey)
                    ? new AzureOpenAIClient(new Uri(aoaiEndpoint), new DefaultAzureCredential(), clientOptions)
                    : new AzureOpenAIClient(new Uri(aoaiEndpoint), new ApiKeyCredential(aoaiKey), clientOptions);

                _evalClient = aoaiClient.GetEvaluationClient();

                _logger.LogInformation("[Foundry] EvaluationClient initialized for {Endpoint}", aoaiEndpoint);
            }
            else
            {
                _logger.LogWarning("[Foundry] No AI endpoint configured  — cloud evaluation disabled");
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Foundry] Failed to initialize EvaluationClient");
        }
    }

    public bool IsAvailable => _evalClient is not null;

    /// <summary>
    /// Creates a Foundry evaluation run for the generated content package.
    /// Returns a <see cref="FoundryEvaluationReport"/>; never throws — errors are logged and null is returned.
    /// </summary>
    public async Task<FoundryEvaluationReport?> SubmitAsync(
        ContentCreatedMessage message, CancellationToken cancellationToken = default)
    {
        if (_evalClient is null)
            return null;

        try
        {
            var evalId = await EnsureEvaluationDefinitionAsync(cancellationToken);

            using var runContent = BinaryContent.Create(BuildRunPayload(evalId, message));
            ClientResult runResult = await _evalClient.CreateEvaluationRunAsync(evalId, runContent);

            using var runDoc = JsonDocument.Parse(runResult.GetRawResponse().Content.ToMemory());
            var root      = runDoc.RootElement;
            var runId     = root.GetProperty("id").GetString() ?? "";
            var runStatus = root.TryGetProperty("status", out var sp) ? sp.GetString() ?? "queued" : "queued";

            _logger.LogInformation(
                "[Foundry] Evaluation run created — eval: {EvalId}, run: {RunId}, status: {Status}",
                evalId, runId, runStatus);

            return new FoundryEvaluationReport
            {
                EvaluationId        = evalId,
                EvaluationRunId     = runId,
                EvaluationRunStatus = runStatus,
                Evaluators          = BuiltInEvaluators,
                SubmittedAt         = DateTimeOffset.UtcNow
            };
        }
        catch (ClientResultException ex) when (ex.Status == 404)
        {
            // The evaluation definition ID is no longer reachable — reset so the
            // next call recreates it under the correct API version.
            _evaluationId = null;
            _logger.LogWarning(ex,
                "[Foundry] Evaluation definition not found (404), cached id reset; topic '{Topic}'",
                message.Topic);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Foundry] Failed to submit evaluation run for topic '{Topic}'",
                message.Topic);
            return null;
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Creates the evaluation definition the first time and caches the ID.
    /// Thread-safe via SemaphoreSlim — subsequent calls return the cached ID immediately.
    /// </summary>
    private async Task<string> EnsureEvaluationDefinitionAsync(CancellationToken ct)
    {
        if (_evaluationId is not null)
            return _evaluationId;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_evaluationId is not null)
                return _evaluationId;

            var payload = BinaryData.FromObjectAsJson(new
            {
                name = EvalDefinitionName,
                data_source_config = new
                {
                    type = "custom",
                    item_schema = new
                    {
                        type = "object",
                        properties = new
                        {
                            query    = new { type = "string" },
                            response = new { type = "string" }
                        },
                        required = new[] { "query", "response" }
                    }
                },
                testing_criteria = new object[]
                {
                    new
                    {
                        // score_model: LLM-as-judge assigns a numeric score 1–5
                        type           = "score_model",
                        name           = "coherence",
                        model          = _modelDeployment,
                        input          = new[]
                        {
                            new
                            {
                                role    = "system",
                                content = "You are an expert technical writer evaluating content quality."
                            },
                            new
                            {
                                role    = "user",
                                content = """
                                    Evaluate the coherence of the response below.
                                    Query: {{item.query}}
                                    Response: {{item.response}}

                                    Rate coherence 1–5 (1=incoherent, 5=perfectly structured and logical).
                                    Reply with only the integer score.
                                    """
                            }
                        },
                        range          = new[] { 1, 5 },
                        pass_threshold = 3
                    },
                    new
                    {
                        // score_model: LLM-as-judge assigns a numeric score 1–5
                        type           = "score_model",
                        name           = "fluency",
                        model          = _modelDeployment,
                        input          = new[]
                        {
                            new
                            {
                                role    = "system",
                                content = "You are an expert technical writer evaluating content quality."
                            },
                            new
                            {
                                role    = "user",
                                content = """
                                    Evaluate the fluency of the response below.
                                    Query: {{item.query}}
                                    Response: {{item.response}}

                                    Rate fluency 1–5 (1=unreadable/grammatically broken, 5=natural and professional).
                                    Reply with only the integer score.
                                    """
                            }
                        },
                        range          = new[] { 1, 5 },
                        pass_threshold = 3
                    }
                }
            });

            using var evalContent = BinaryContent.Create(payload);
            ClientResult result = await _evalClient!.CreateEvaluationAsync(evalContent);

            using var doc = JsonDocument.Parse(result.GetRawResponse().Content.ToMemory());
            _evaluationId = doc.RootElement.GetProperty("id").GetString()
                ?? throw new InvalidOperationException("Foundry returned no evaluation id");

            _logger.LogInformation("[Foundry] Evaluation definition created — id: {Id}", _evaluationId);
            return _evaluationId;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Builds the evaluation run JSON payload with two inline items:
    ///   • Blog item    — query = topic prompt, response = blog markdown (truncated to 2 000 chars)
    ///   • Social item  — query = LinkedIn prompt, response = LinkedIn text
    /// </summary>
    private static BinaryData BuildRunPayload(string evalId, ContentCreatedMessage message)
    {
        var blogPreview = message.BlogMarkdown.Length > 2000
            ? message.BlogMarkdown[..2000] + "..."
            : message.BlogMarkdown;

        return BinaryData.FromObjectAsJson(new
        {
            name = $"content-eval-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
            data_source = new
            {
                type = "jsonl",
                source = new
                {
                    type = "file_content",
                    content = new[]
                    {
                        new
                        {
                            item = new
                            {
                                query    = $"Write a comprehensive technical blog post about: {message.Topic}",
                                response = blogPreview
                            }
                        },
                        new
                        {
                            item = new
                            {
                                query    = $"Write an engaging LinkedIn post for developers about: {message.Topic}",
                                response = message.LinkedIn
                            }
                        }
                    }
                }
            }
        });
    }
}
