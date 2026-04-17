using System.Text.Json.Serialization;

namespace AgentCreator.Models;

/// <summary>
/// Event published to Dapr pubsub when the content creation pipeline completes.
/// Consumed by agent-evaluator to trigger quality evaluation.
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
