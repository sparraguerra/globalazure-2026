using System.ClientModel;
using AgentEvaluator.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentEvaluator;

/// <summary>
/// Performs LLM-as-judge evaluation of AI-generated content using a ChatClientAgent.
/// Follows the same MAF pattern as agent-creator executors: owns its agent and manages sessions.
/// </summary>
public class ContentEvaluationService
{
    private const int MaxRetries = 3;
    private readonly AIAgent? _agent;
    private readonly ILogger<ContentEvaluationService> _logger;

    public ContentEvaluationService(EvaluatorAgent evaluatorAgent, ILogger<ContentEvaluationService> logger)
    {
        _logger = logger;
        if (evaluatorAgent.ChatClient is null)
        {
            return;
        }
            
        _agent = new ChatClientAgent(
            evaluatorAgent.ChatClient,
            name: "evaluator-agent",
            instructions: """
                You are an expert content quality evaluator specializing in technical developer content.
                Your role is to assess the quality of AI-generated blog posts and social media content.
                Provide objective, numerical scores between 1.0 and 10.0 and actionable feedback.
                Be concise, specific, and constructive in your assessments.
                """)
            .AsBuilder()
            .UseOpenTelemetry(sourceName: "evaluator-agent", configure: c => c.EnableSensitiveData = true)
            .Build();
        _logger = logger;
    }

    public async Task<EvaluationResult> EvaluateAsync(
        ContentCreatedMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Evaluator] Evaluating content for topic: '{Topic}'", message.Topic);

        if (_agent is null)
        {
            _logger.LogWarning("[Evaluator] No LLM configured — returning fallback scores");
            return BuildFallbackResult(message.Topic);
        }

        try
        {
            var session = await _agent.CreateSessionAsync(cancellationToken);
            var prompt = BuildEvaluationPrompt(message);
            var response = await AgentRunWithRetry(_agent, prompt, session, cancellationToken);
            var result = ParseEvaluationResponse(response.Text ?? "", message.Topic);
            Console.WriteLine(
                $"[Evaluator] Complete — blog: {result.BlogQualityScore:F1}, " +
                $"social: {result.SocialQualityScore:F1}, " +
                $"relevance: {result.RelevanceScore:F1}, " +
                $"overall: {result.OverallScore:F1}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Evaluator] LLM evaluation failed");
            return BuildFallbackResult(message.Topic);
        }
    }

    private static string BuildEvaluationPrompt(ContentCreatedMessage message)
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

    private static EvaluationResult ParseEvaluationResponse(string text, string topic)
    {
        var blogScore = ParseScore(text, "BLOG_SCORE:");
        var socialScore = ParseScore(text, "SOCIAL_SCORE:");
        var relevanceScore = ParseScore(text, "RELEVANCE_SCORE:");
        var blogFeedback = ExtractLine(text, "BLOG_FEEDBACK:");
        var socialFeedback = ExtractLine(text, "SOCIAL_FEEDBACK:");
        var recommendations = ExtractLine(text, "RECOMMENDATIONS:")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (blogScore <= 0 || socialScore <= 0 || relevanceScore <= 0)
            throw new InvalidOperationException("Failed to parse evaluation scores from LLM response");

        return new EvaluationResult
        {
            Topic = topic,
            BlogQualityScore = blogScore,
            SocialQualityScore = socialScore,
            RelevanceScore = relevanceScore,
            OverallScore = Math.Round((blogScore + socialScore + relevanceScore) / 3.0, 1),
            BlogFeedback = blogFeedback,
            SocialFeedback = socialFeedback,
            Recommendations = recommendations,
            EvaluatedAt = DateTimeOffset.UtcNow
        };
    }

    private static double ParseScore(string text, string marker)
    {
        var idx = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return 0;
        var rest = text[(idx + marker.Length)..].TrimStart();
        var end = rest.IndexOfAny(['\r', '\n']);
        var scoreStr = (end >= 0 ? rest[..end] : rest).Trim();
        return double.TryParse(scoreStr,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var score) ? score : 0;
    }

    private static string ExtractLine(string text, string marker)
    {
        var idx = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return "";
        var rest = text[(idx + marker.Length)..].TrimStart();
        var end = rest.IndexOfAny(['\r', '\n']);
        return (end >= 0 ? rest[..end] : rest).Trim();
    }

    private static EvaluationResult BuildFallbackResult(string topic) =>
        new()
        {
            Topic = topic,
            BlogQualityScore = 7.0,
            SocialQualityScore = 7.0,
            RelevanceScore = 7.0,
            OverallScore = 7.0,
            BlogFeedback = "Evaluation unavailable — LLM not configured.",
            SocialFeedback = "Evaluation unavailable — LLM not configured.",
            Recommendations = ["Configure AZURE_AI_PROJECT_ENDPOINT or AZURE_OPENAI_ENDPOINT for LLM-based evaluation"],
            EvaluatedAt = DateTimeOffset.UtcNow
        };

    private async Task<AgentResponse> AgentRunWithRetry(
        AIAgent agent, string prompt, AgentSession session, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                return await agent.RunAsync(prompt, session, cancellationToken: ct);
            }
            catch (ClientResultException ex) when (ex.Status == 429 && attempt < MaxRetries)
            {
                var waitSeconds = 60 * attempt;
                _logger.LogWarning("[Evaluator] Rate limited (429). Retrying in {WaitSeconds}s (attempt {Attempt}/{MaxRetries})...", waitSeconds, attempt, MaxRetries);
                await Task.Delay(TimeSpan.FromSeconds(waitSeconds), ct);
            }
        }
        return await agent.RunAsync(prompt, session, cancellationToken: ct);
    }
}
