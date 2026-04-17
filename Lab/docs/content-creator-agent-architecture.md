# Content Creator Agent Architecture

.NET agent on [Microsoft Agent Framework](https://github.com/microsoft/agent-framework) that transforms a research brief into a content package (blog + LinkedIn + Social thread). Follows the [CustomAgentExecutors](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/03-workflows/Agents/CustomAgentExecutors) pattern: each executor owns its own `ChatClientAgent` + `AgentSession` and calls `RunAsync(prompt, session)` directly.

**Stack:** C# / .NET 10 / ASP.NET Minimal API / `Microsoft.Agents.AI` + `Microsoft.Agents.AI.Workflows` / Azure OpenAI / OpenTelemetry

## Architecture Diagram

```mermaid
---
config:
  theme: base
  themeVariables:
    lineColor: "#333"
    primaryColor: "#fff"
  flowchart:
    curve: basis
---
graph TB
    subgraph A2A["A2A Interface"]
        Client(["Client<br/>tasks/send · SendMessage · message/send"])
        Card["/.well-known/agent.json<br/>A2A v0.3.0 · 2 skills"]
    end

    subgraph Workflow["WorkflowBuilder Pipeline (.NET 10 · MAF)"]
        direction LR
        E1["BriefInputExecutor<br/>Validate brief<br/>extract topic"]
        E2["BlogGenerationExecutor<br/>blog-writer ChatClientAgent<br/>800–1 200 word article"]
        E3["SocialGenerationExecutor<br/>social-writer ChatClientAgent<br/>LinkedIn + 3-post thread"]
        E4["OutputExecutor<br/>Assemble content_package"]
    end

    subgraph Ctx["IWorkflowContext State"]
        direction LR
        S1["topic · sources"]
        S2["blogResult"]
        S3["socialResult"]
    end

    subgraph AI["Azure OpenAI / Foundry"]
        AOAI["GPT-4o<br/>LLM completions<br/>retry · fallback"]
    end

    subgraph Out["Output"]
        CP["content_package<br/>blog_post · linkedin · social_thread"]
    end

    OTel["OpenTelemetry<br/>creator-agent source · OTLP/gRPC"]

    Client ==>|"/a2a or /pipeline"| E1
    E1 ==> E2
    E2 ==> E3
    E3 ==> E4
    E4 ==> CP

    E1 -. "write" .-> S1
    E2 -. "read topic / write blogResult" .-> S2
    E3 -. "read blogResult / write socialResult" .-> S3

    E2 --> AOAI
    E3 --> AOAI

    E2 -. "spans" .-> OTel
    E3 -. "spans" .-> OTel

    style A2A      fill:#0f2027,stroke:#0078D4,stroke-width:3px,color:#fff
    style Workflow fill:#0f2027,stroke:#28a745,stroke-width:3px,color:#fff
    style Ctx      fill:#0f2027,stroke:#e6a800,stroke-width:3px,color:#fff
    style AI       fill:#0f2027,stroke:#e6a800,stroke-width:3px,color:#fff
    style Out      fill:#0f2027,stroke:#e74c3c,stroke-width:3px,color:#fff

    style Client fill:#0078D4,stroke:#004E8C,stroke-width:2px,color:#fff
    style Card   fill:#444,stroke:#aaa,stroke-width:2px,color:#fff

    style E1 fill:#1a7340,stroke:#28a745,stroke-width:2px,color:#fff
    style E2 fill:#1a7340,stroke:#28a745,stroke-width:2px,color:#fff
    style E3 fill:#1a7340,stroke:#28a745,stroke-width:2px,color:#fff
    style E4 fill:#1a7340,stroke:#28a745,stroke-width:2px,color:#fff

    style S1 fill:#7a5800,stroke:#e6a800,stroke-width:2px,color:#fff
    style S2 fill:#7a5800,stroke:#e6a800,stroke-width:2px,color:#fff
    style S3 fill:#7a5800,stroke:#e6a800,stroke-width:2px,color:#fff

    style AOAI fill:#FFB900,stroke:#C08000,stroke-width:2px,color:#000

    style CP   fill:#922b21,stroke:#e74c3c,stroke-width:2px,color:#fff
    style OTel fill:#5b2c6f,stroke:#8e44ad,stroke-width:2px,color:#fff
```

## Executor-Owned Agents (CustomAgentExecutors Pattern)

Each workflow executor constructs its own `ChatClientAgent` with role-specific `instructions` and manages its own `AgentSession` — following the MAF best practice where executors own the agent lifecycle:

```csharp
// Inside BlogGenerationExecutor constructor
_agent = new ChatClientAgent(chatClient,
        name: "blog-writer",
        instructions: "You are an expert technical writer creating original content...")
    .AsBuilder()
    .UseOpenTelemetry(sourceName: "creator-agent", configure: c => c.EnableSensitiveData = true)
    .Build();
```

Each executor creates a session and invokes the agent through the proper `AIAgent.RunAsync` API:

```csharp
_session ??= await _agent.CreateSessionAsync(cancellationToken);
var response = await ContentCreatorAgent.AgentRunWithRetry(
    _agent, userPrompt, _session, cancellationToken);
blogMarkdown = response.Text;
```

This ensures the full MAF middleware pipeline fires (OTel, logging) and the agent tracks conversation via `AgentSession`.

## Workflow Pipeline (`/pipeline`)

Four `Executor<TIn,TOut>` steps chained via `WorkflowBuilder`. State flows through `IWorkflowContext`:

```csharp
var workflowBuilder = new WorkflowBuilder(briefInput)
    .AddEdge(briefInput, blogGeneration)
    .AddEdge(blogGeneration, socialGeneration)
    .AddEdge(socialGeneration, output)
    .WithOutputFrom(output);
await using var run = await InProcessExecution.RunStreamingAsync(workflow, researchBrief);
```

| Executor | In → Out | Agent | Role |
|----------|----------|-------|------|
| `BriefInputExecutor` | `JsonElement` → `JsonElement` | — | Validates brief has `topic` |
| `BlogGenerationExecutor` | `JsonElement` → `BlogResult` | `blog-writer` | Owns agent + session. Builds user prompt from parsed sources. 800–1200 word article. OTel spans emit under `creator-agent`. |
| `SocialGenerationExecutor` | `BlogResult` → `SocialResult` | `social-writer` | Owns agent + session. LinkedIn + 3-post thread from blog text. OTel spans emit under `creator-agent`. |
| `OutputExecutor` | `SocialResult` → `object` | — | Reads all state, assembles `content_package` |

State passing between executors uses `IWorkflowContext`:

```csharp
// BlogGenerationExecutor writes
await context.QueueStateUpdateAsync("topic", parsed.Topic, cancellationToken);
await context.QueueStateUpdateAsync("blogResult", result, cancellationToken);

// SocialGenerationExecutor reads
var topic = await context.ReadStateAsync<string>("topic", cancellationToken);
```

**Retry/Fallback:** 429 → exponential backoff (60s × attempt, max 3). LLM failure → template-based fallback that groups sources by type.

The `/a2a` endpoint bypasses the workflow — calls `CreateContentAsync` directly (runs both agents sequentially in one method).

## Output

```json
{
  "content_package": {
    "blog_post": { "title": "...", "markdown": "...", "word_count": 950, "sources_used": 18 },
    "social": {
      "linkedin": { "text": "...", "char_count": 230 },
      "social_thread": ["(1/3)...", "(2/3)...", "(3/3)..."]
    }
  }
}
```

## A2A Exposure

ASP.NET Minimal API. JSON-RPC methods `tasks/send`, `SendMessage`, `message/send` on `/a2a`. Agent card at `/.well-known/agent.json` (A2A v0.3.0). Two skills: `create-content` (implemented), `revise-content` (declared). Optional auth via `A2AAuthFilter` — Bearer token or `X-API-Key`.

## Observability

OTel sources: `creator-agent`, `content-agent`, `content-factory-workflow`, `*Microsoft.Extensions.AI`, `*Microsoft.Agents.AI`. Both executors emit spans under the shared `creator-agent` source for Foundry compatibility — spans trace through `AIAgent.RunAsync` → `ChatClientAgent` → `IChatClient`. Exports via OTLP/gRPC.
