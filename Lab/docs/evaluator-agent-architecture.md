# Evaluator Agent Architecture

.NET agent on [Microsoft Agent Framework](https://github.com/microsoft/agent-framework) that automatically evaluates AI-generated content packages produced by the Content Creator Agent. Triggered via **Dapr pub/sub** (`content-created` topic) and runs two complementary evaluation paths: a fast **LLM-as-judge** (local) via `ContentEvaluationService` and an async **cloud evaluation** via `FoundryEvaluationService` that submits runs to the Azure AI Foundry Evals API.

**Stack:** C# / .NET 10 / ASP.NET Minimal API / Dapr / `Microsoft.Agents.AI` / Azure OpenAI / Azure AI Foundry Evals / OpenTelemetry

## Internal Architecture

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
    subgraph Trigger["Event Trigger"]
        DaprSub["Dapr Subscriber<br/>POST /content-created<br/>topic: content-created"]
    end

    subgraph Core["Evaluator Agent (.NET 10 · MAF)"]
        direction TB
        EA["EvaluatorAgent<br/>IChatClient init<br/>Foundry · AOAI fallback"]
        CES["ContentEvaluationService<br/>LLM-as-judge · MAF ChatClientAgent<br/>local · fast"]
        FES["FoundryEvaluationService<br/>Cloud eval · Evals API<br/>async · Foundry portal"]
    end

    subgraph Output["Evaluation Output"]
        ER["EvaluationResult<br/>blog_score · social_score<br/>relevance_score · feedback"]
        FR["FoundryEvaluationReport<br/>eval_id · run_id"]
    end

    subgraph AI["Azure AI"]
        AOAI["Azure OpenAI / Foundry<br/>GPT-4o · judge model"]
        FoundryEvals["Azure AI Foundry<br/>Evaluations Portal"]
    end

    subgraph A2ACard["A2A Interface"]
        Card["/.well-known/agent.json<br/>content-evaluator-agent · v1.0.0"]
    end

    DaprSub --> CES
    DaprSub --> FES
    EA --> CES
    EA --> FES
    CES --> AOAI
    FES --> AOAI
    CES --> ER
    FES --> FR
    FR --> FoundryEvals

    style Trigger  fill:#0f2027,stroke:#0078D4,stroke-width:3px,color:#fff
    style Core     fill:#0f2027,stroke:#28a745,stroke-width:3px,color:#fff
    style Output   fill:#0f2027,stroke:#e6a800,stroke-width:3px,color:#fff
    style AI       fill:#0f2027,stroke:#e6a800,stroke-width:3px,color:#fff
    style A2ACard  fill:#0f2027,stroke:#666,stroke-width:3px,color:#fff

    style DaprSub      fill:#0078D4,stroke:#004E8C,stroke-width:2px,color:#fff
    style EA           fill:#1a7340,stroke:#28a745,stroke-width:2px,color:#fff
    style CES          fill:#1a7340,stroke:#28a745,stroke-width:2px,color:#fff
    style FES          fill:#1a7340,stroke:#28a745,stroke-width:2px,color:#fff
    style ER           fill:#FFB900,stroke:#C08000,stroke-width:2px,color:#000
    style FR           fill:#922b21,stroke:#e74c3c,stroke-width:2px,color:#fff
    style AOAI         fill:#FFB900,stroke:#C08000,stroke-width:2px,color:#000
    style FoundryEvals fill:#922b21,stroke:#e74c3c,stroke-width:2px,color:#fff
    style Card         fill:#444,stroke:#aaa,stroke-width:2px,color:#fff
```

## Evaluation Services

### `ContentEvaluationService` — LLM-as-judge

Follows the same MAF executor pattern as the Content Creator: owns a `ChatClientAgent` + `AgentSession`, invokes it with a structured prompt, and parses the plain-text response.

```csharp
_agent = new ChatClientAgent(
    evaluatorAgent.ChatClient,
    name: "evaluator-agent",
    instructions: "You are an expert content quality evaluator...")
  .AsBuilder()
  .UseOpenTelemetry(sourceName: "evaluator-agent", configure: c => c.EnableSensitiveData = true)
  .Build();
```

Prompt format enforces strict structured output:

```
BLOG_SCORE: <1.0–10.0>
SOCIAL_SCORE: <1.0–10.0>
RELEVANCE_SCORE: <1.0–10.0>
BLOG_FEEDBACK: <2-3 sentences>
SOCIAL_FEEDBACK: <2-3 sentences>
RECOMMENDATIONS: <semicolon-separated list>
```

**Retry/Fallback:** up to 3 retries on transient errors; returns neutral fallback scores (5.0) if the LLM is unavailable.

### `FoundryEvaluationService` — Cloud Evaluation

Submits the content package to Azure AI Foundry using the OpenAI Evals API (`EvaluationClient`):

1. **Create evaluation definition** (cached per process start) with `builtin.coherence` + `builtin.fluency` evaluators.
2. **Create evaluation run** with inline items containing the blog post and LinkedIn post.
3. Returns a `FoundryEvaluationReport` — results surface in the Foundry portal.

Supports two authentication modes:
| Mode | Env Vars | Auth |
|------|----------|------|
| Azure AI Foundry project | `AZURE_AI_PROJECT_ENDPOINT` | `DefaultAzureCredential` (Managed Identity / `az login`) |
| Azure OpenAI direct | `AZURE_OPENAI_ENDPOINT` + `AZURE_OPENAI_API_KEY` | `ApiKeyCredential` |

API version pinned to `2025-04-01-preview` — first version exposing `POST /openai/evals/{id}/runs`.

## Event Flow

```mermaid
sequenceDiagram
    participant Creator as Content Creator Agent
    participant Dapr as Dapr pubsub<br/>(Redis)
    participant Evaluator as Evaluator Agent
    participant AOAI as Azure OpenAI / Foundry
    participant FPortal as Foundry Evals Portal

    Creator->>Dapr: publish content-created<br/>{topic, blog, linkedin, tweets}
    Dapr->>Evaluator: POST /content-created (CloudEvent)
    par LLM-as-judge
        Evaluator->>AOAI: chat completion (judge prompt)
        AOAI-->>Evaluator: BLOG_SCORE · SOCIAL_SCORE · RELEVANCE_SCORE
    and Cloud evaluation
        Evaluator->>AOAI: POST /evals/{id}/runs (inline items)
        AOAI-->>Evaluator: eval_id · run_id
        Evaluator-->>FPortal: results appear asynchronously
    end
    Evaluator-->>Evaluator: log combined EvaluationResult
```

## Output

```json
{
  "topic": "Azure Container Apps",
  "blog_quality_score": 8.2,
  "social_quality_score": 7.5,
  "relevance_score": 9.0,
  "overall_score": 8.2,
  "blog_feedback": "...",
  "social_feedback": "...",
  "recommendations": ["Add code samples", "Include pricing references"],
  "foundry_report": {
    "evaluation_id": "eval_...",
    "evaluation_run_id": "run_..."
  }
}
```

## A2A Interface

Exposes a standard A2A Agent Card at `/.well-known/agent.json` and `/.well-known/agent-card.json`:

| Field | Value |
|-------|-------|
| Name | `content-evaluator-agent` |
| Skill | `evaluate-content` |
| Transport | JSONRPC |
| Input | `application/json` |
| Output | `application/json` |
| Auth | Bearer (when `A2A_AUTH_ENABLED=true`) |

## Runtime Switch (Local vs Foundry Agent)

The evaluator supports a runtime toggle to switch execution backend without changing code:

| Env Var | Values | Effect |
|---------|--------|--------|
| `EVALUATOR_EXECUTION_MODE` | `local` (default) | Uses `ContentEvaluationService` + optional `FoundryEvaluationService` run submission |
| `EVALUATOR_EXECUTION_MODE` | `foundry-agent` | Calls a published Foundry agent through `FOUNDRY_EVALUATOR_RESPONSES_ENDPOINT` |

When running in `foundry-agent` mode, configure:

- `FOUNDRY_EVALUATOR_RESPONSES_ENDPOINT` (required)
- one auth option: `FOUNDRY_EVALUATOR_API_KEY` or `FOUNDRY_EVALUATOR_BEARER_TOKEN`
- optional: `FOUNDRY_EVALUATOR_MODEL`
- optional credential scope: `FOUNDRY_EVALUATOR_SCOPE` (default `https://ai.azure.com/.default`)

## Observability

All evaluation spans are emitted under the `evaluator-agent` source and forwarded via OTLP to the Managed OTEL Collector → Application Insights:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("evaluator-agent")
        .AddSource("*Microsoft.Extensions.AI")
        .AddSource("*Microsoft.Agents.AI")
        .AddSource("*Microsoft.Extensions.Agents*"));
```
