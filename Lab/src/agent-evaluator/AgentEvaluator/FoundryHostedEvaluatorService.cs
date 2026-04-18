using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AgentEvaluator.Models;
using Azure.Core;
using Azure.Identity;

namespace AgentEvaluator;

/// <summary>
/// Calls a published evaluator agent in Azure AI Foundry through a Responses API endpoint.
/// This is used when EVALUATOR_EXECUTION_MODE is set to "foundry-agent".
/// </summary>
public class FoundryHostedEvaluatorService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FoundryHostedEvaluatorService> _logger;
    private readonly DefaultAzureCredential _credential = new();

    private readonly string? _endpoint;
    private readonly string? _model;
    private readonly string? _apiKey;
    private readonly string? _bearerToken;
    private readonly string _scope;

    public FoundryHostedEvaluatorService(HttpClient httpClient, ILogger<FoundryHostedEvaluatorService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _endpoint = Environment.GetEnvironmentVariable("FOUNDRY_EVALUATOR_RESPONSES_ENDPOINT");
        _model = Environment.GetEnvironmentVariable("FOUNDRY_EVALUATOR_MODEL");
        _apiKey = Environment.GetEnvironmentVariable("FOUNDRY_EVALUATOR_API_KEY");
        _bearerToken = Environment.GetEnvironmentVariable("FOUNDRY_EVALUATOR_BEARER_TOKEN");
        _scope = Environment.GetEnvironmentVariable("FOUNDRY_EVALUATOR_SCOPE") ?? "https://ai.azure.com/.default";
    }

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_endpoint);

    public async Task<EvaluationResult> EvaluateAsync(ContentCreatedMessage message, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            _logger.LogWarning("[Evaluator/FoundryAgent] Missing FOUNDRY_EVALUATOR_RESPONSES_ENDPOINT; returning fallback scores");
            return ContentEvaluationService.BuildFallbackResult(message.Topic);
        }

        var prompt = BuildRemotePrompt(message);
        var request = BuildRequest(prompt);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(request, Encoding.UTF8, "application/json")
        };

        await AttachAuthHeadersAsync(httpRequest, cancellationToken);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "[Evaluator/FoundryAgent] Request failed with status {StatusCode}. Body: {Body}",
                (int)response.StatusCode,
                body.Length > 1000 ? body[..1000] + "..." : body);
            return ContentEvaluationService.BuildFallbackResult(message.Topic);
        }

        try
        {
            var responseText = TryExtractResponseText(body);
            if (string.IsNullOrWhiteSpace(responseText))
            {
                _logger.LogWarning("[Evaluator/FoundryAgent] Empty output from remote agent; returning fallback scores");
                return ContentEvaluationService.BuildFallbackResult(message.Topic);
            }

            return ContentEvaluationService.ParseEvaluationResponse(responseText, message.Topic);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Evaluator/FoundryAgent] Failed to parse remote output; returning fallback scores");
            return ContentEvaluationService.BuildFallbackResult(message.Topic);
        }
    }

    private string BuildRequest(string prompt)
    {
        var payload = new Dictionary<string, object?>
        {
            ["input"] = new object[]
            {
                new
                {
                    role = "system",
                    content = new[]
                    {
                        new { type = "input_text", text = "You are a strict content evaluator. Follow the output format exactly." }
                    }
                },
                new
                {
                    role = "user",
                    content = new[]
                    {
                        new { type = "input_text", text = prompt }
                    }
                }
            }
        };

        if (!string.IsNullOrWhiteSpace(_model))
            payload["model"] = _model;

        return JsonSerializer.Serialize(payload);
    }

    private static string BuildRemotePrompt(ContentCreatedMessage message)
    {
        var tweetBlock = string.Join("\n", message.Tweets.Select((t, i) => $"[{i + 1}] {t}"));
        var blogPreview = message.BlogMarkdown.Length > 2000
            ? message.BlogMarkdown[..2000] + "..."
            : message.BlogMarkdown;

        return $"""
            Evaluate the following AI-generated content package for topic: "{message.Topic}"
            ({message.WordCount} words, {message.SourcesUsed} sources used)

            Return EXACTLY in this format (no extra text before or after):

            BLOG_SCORE: <number 1.0-10.0>
            SOCIAL_SCORE: <number 1.0-10.0>
            RELEVANCE_SCORE: <number 1.0-10.0>
            BLOG_FEEDBACK: <2-3 sentences of specific feedback on the blog post quality>
            SOCIAL_FEEDBACK: <2-3 sentences of specific feedback on the social media content>
            RECOMMENDATIONS: <semicolon-separated list of 2-3 actionable improvements>

            Evaluation criteria:
            - Blog quality (1-10): technical accuracy, structure, depth, clarity, originality
            - Social quality (1-10): engagement potential, appropriate length, hashtag relevance
            - Relevance (1-10): how well the content addresses the stated topic

            === BLOG POST ===
            {blogPreview}

            === LINKEDIN POST ===
            {message.LinkedIn}

            === TWITTER THREAD ===
            {tweetBlock}
            """;
    }

    private static string? TryExtractResponseText(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        if (root.TryGetProperty("output_text", out var outputText) && outputText.ValueKind == JsonValueKind.String)
            return outputText.GetString();

        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var contentItem in content.EnumerateArray())
            {
                if (contentItem.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                    return text.GetString();
            }
        }

        return null;
    }

    private async Task AttachAuthHeadersAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_apiKey))
            request.Headers.Add("api-key", _apiKey);

        if (!string.IsNullOrWhiteSpace(_bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _bearerToken);
            return;
        }

        if (!string.IsNullOrWhiteSpace(_apiKey))
            return;

        var token = await _credential.GetTokenAsync(new TokenRequestContext([_scope]), cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
    }
}
