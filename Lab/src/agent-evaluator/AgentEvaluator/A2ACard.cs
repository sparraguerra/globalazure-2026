using AgentEvaluator.Models;

namespace AgentEvaluator;

/// <summary>A2A Agent Card for the Content Creator Agent.</summary>
public static class A2ACard
{
    public static AgentCard ForBaseUrl(string baseUrl)
    {
        var authEnabled = string.Equals(
            Environment.GetEnvironmentVariable("A2A_AUTH_ENABLED"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        var card = new AgentCard
        {
            Name = "content-evaluator-agent",
            Description = "Evaluates research briefs and provides feedback",
            Url = $"{baseUrl}/a2a",
            Version = "1.0.0",
            ProtocolVersion = "0.3.0",
            PreferredTransport = "JSONRPC",
            SupportedInterfaces = [],
            Capabilities = new AgentCapabilities { Streaming = false, PushNotifications = false },
            DefaultInputModes = ["text/plain", "application/json"],
            DefaultOutputModes = ["text/plain", "application/json"],
            Skills =
            [
                new AgentSkill
                {
                    Id = "evaluate-content",
                    Name = "Evaluate Content",
                    Description = "Evaluates research briefs and provides feedback",
                    Tags = ["content", "evaluation", "feedback"],
                    InputModes = ["application/json"],
                    OutputModes = ["application/json"]
                }
            ]
        };

        // Add security schemes only if auth is enabled
        if (authEnabled)
        {
            card = card with
            {
                SecuritySchemes = new Dictionary<string, AuthScheme>
                {
                    ["BearerAuth"] = new AuthScheme
                    {
                        Type = "http",
                        Scheme = "bearer",
                        BearerFormat = "SharedSecret"
                    },
                    ["ApiKeyAuth"] = new AuthScheme
                    {
                        Type = "apiKey",
                        In = "header",
                        Name = "X-API-Key"
                    }
                },
                Security =
                [
                    new Dictionary<string, string[]> { ["BearerAuth"] = [] },
                    new Dictionary<string, string[]> { ["ApiKeyAuth"] = [] }
                ]
            };
        }

        return card;
    }
}
