using Azure.AI.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.AI;
using System.ClientModel;

namespace AgentEvaluator;

/// <summary>
/// Initializes the IChatClient for the evaluator agent.
/// Supports two modes:
///   1. Azure AI Foundry project endpoint (AZURE_AI_PROJECT_ENDPOINT) with DefaultAzureCredential.
///   2. Azure OpenAI direct (AZURE_OPENAI_ENDPOINT + AZURE_OPENAI_API_KEY) as fallback.
/// </summary>
public class EvaluatorAgent
{
    public IChatClient? ChatClient { get; private set; }

    public EvaluatorAgent()
    {
        try
        {
            var deployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT")
                ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
                ?? "gpt-4o";

            var foundryEndpoint = Environment.GetEnvironmentVariable("AZURE_AI_PROJECT_ENDPOINT");
            var aoaiEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
            var aoaiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");

            if (!string.IsNullOrEmpty(foundryEndpoint))
            {
                // Azure AI Foundry: authenticate via DefaultAzureCredential (managed identity / az login)
                var projectClientOptions = new AIProjectClientOptions(AIProjectClientOptions.ServiceVersion.V2025_05_01);
                var projectClient = new AIProjectClient(new Uri(foundryEndpoint), new DefaultAzureCredential(), projectClientOptions);
                var aoaiClient = projectClient.GetProjectOpenAIClient();

                ChatClient = aoaiClient.GetChatClient(deployment)
                    .AsIChatClient()
                    .AsBuilder()
                    .UseOpenTelemetry(sourceName: "evaluator-agent", configure: c => c.EnableSensitiveData = true)
                    .Build();
                Console.WriteLine("[EvaluatorAgent] Initialized via Azure AI Foundry");
            }
            else if (!string.IsNullOrEmpty(aoaiEndpoint) && !string.IsNullOrEmpty(aoaiKey))
            {
                var credential = new ApiKeyCredential(aoaiKey);
                var aoaiClient = new AzureOpenAIClient(new Uri(aoaiEndpoint), credential);
                ChatClient = aoaiClient.GetChatClient(deployment)
                    .AsIChatClient()
                    .AsBuilder()
                    .UseOpenTelemetry(sourceName: "evaluator-agent", configure: c => c.EnableSensitiveData = true)
                    .Build();
                Console.WriteLine("[EvaluatorAgent] Initialized via Azure OpenAI direct");
            }
            else
            {
                Console.WriteLine("[EvaluatorAgent] No AI endpoint configured — evaluation will use fallback scores");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[EvaluatorAgent] IChatClient initialization failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
