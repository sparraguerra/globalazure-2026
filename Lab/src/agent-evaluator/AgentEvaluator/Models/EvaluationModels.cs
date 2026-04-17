using System.Text.Json.Serialization;

namespace AgentEvaluator.Models;

/// <summary>
/// Message published by agent-creator via Dapr pubsub when content generation completes.
/// </summary>
public record ContentCreatedMessage
{
    [JsonPropertyName("topic")]
    public string Topic { get; init; } = "";

    [JsonPropertyName("blogMarkdown")]
    public string BlogMarkdown { get; init; } = "";

    [JsonPropertyName("wordCount")]
    public int WordCount { get; init; }

    [JsonPropertyName("sourcesUsed")]
    public int SourcesUsed { get; init; }

    [JsonPropertyName("linkedIn")]
    public string LinkedIn { get; init; } = "";

    [JsonPropertyName("tweets")]
    public List<string> Tweets { get; init; } = [];

    [JsonPropertyName("generatedAt")]
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Result of an LLM-as-judge evaluation of a generated content package.
/// </summary>
public record EvaluationResult
{
    public string Topic { get; init; } = "";
    public double BlogQualityScore { get; init; }
    public double SocialQualityScore { get; init; }
    public double RelevanceScore { get; init; }
    public double OverallScore { get; init; }
    public string BlogFeedback { get; init; } = "";
    public string SocialFeedback { get; init; } = "";
    public string[] Recommendations { get; init; } = [];
    public FoundryEvaluationReport? FoundryReport { get; init; }
    public DateTimeOffset EvaluatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Reference to the cloud evaluation run created in Azure AI Foundry.
/// The evaluation results (coherence, fluency) appear in the Foundry portal
/// under Evaluation → Runs for the connected Azure OpenAI resource.
/// </summary>
public record FoundryEvaluationReport
{
    public string EvaluationId { get; init; } = "";
    public string EvaluationRunId { get; init; } = "";
    public string EvaluationRunStatus { get; init; } = "";
    public string[] Evaluators { get; init; } = [];
    public DateTimeOffset SubmittedAt { get; init; } = DateTimeOffset.UtcNow;
}
