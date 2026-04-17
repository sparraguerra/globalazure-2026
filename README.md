# 4 AI Agents, Real Tools and Models -- All on Azure Container Apps with Foundry

A multi-agent content factory that researches Microsoft technology topics, creates multi-format content, and generates podcasts. Built to familiarize with running and hosting AI Agents on Azure Container Apps, explore agent observability, and register and evaluate agents in Microsoft Foundry.

## Architecture

```
[Dev UI] --> [Agent 1 - Researcher] --> [Agent 2 - Content Creator] --> [Agent 3 - Podcaster]
                                                  |
                                         [Agent 4 - Evaluator] (via Dapr pub/sub)
```

Enter a topic (e.g. *"Write a comprehensive blog post about Azure Container Apps for developers"*). Four agents collaborate:

1. **Agent 1 — Tech Research** (LangGraph / Python) — Searches Microsoft Learn, Azure Blog, Tech Community, Azure Updates, and GitHub. Uses AI for intent detection, ranks sources by relevance, fetches full content from top hits, follows depth-1 links from trusted domains, and synthesizes a research brief.
2. **Agent 2 — Content Creator** (Microsoft Agent Framework / .NET) — Transforms the research brief into an original blog post, LinkedIn post, and Twitter thread, all grounded in real sources.
3. **Agent 3 — Podcaster** (GitHub Copilot SDK / Python) — Creates an engaging podcast script and generates audio. Can use Azure OpenAI TTS or a custom XTTS-v2 server on serverless GPUs.
4. **Agent 4 — Evaluator** (Microsoft Agent Framework / .NET) — Automatically evaluates AI-generated content packages. Triggered via **Dapr pub/sub** (`content-created` topic). Runs a fast **LLM-as-judge** evaluation locally and submits async **cloud evaluations** to the Azure AI Foundry Evals API.

**Dev UI** — A lightweight HTML frontend for submitting topics and viewing results from all three agents.

Agents communicate via the **A2A (Agent-to-Agent) protocol** — each exposes a `/.well-known/agent.json` card for discovery and a `/a2a` JSON-RPC endpoint for task submission. Each agent runs as a separate container on Azure Container Apps.

## Deploy to Azure

```bash
azd up
```

This creates: Azure AI Foundry (GPT-4o + TTS), ACR, ACA Environment, Log Analytics, Storage Account, and Container Apps for each agent plus the Dev UI.

## Run Locally

With Docker Compose:

```bash
cd Lab
cp .env.example .env  # fill in your Azure OpenAI credentials
docker compose up     # starts agents 1-3 + dev-ui
```

Without Docker, see the step-by-step guide: [Lab/docs/how-to-run-locally.md](Lab/docs/how-to-run-locally.md).

## Talk Materials

- [Lab/docs/stop-building-apis-forge-agents-talk-guide.md](Lab/docs/stop-building-apis-forge-agents-talk-guide.md) — project explanation, demo narrative, and audience Q&A for the session.
- [Lab/docs/stop-building-apis-forge-agents-speaker-summary.md](Lab/docs/stop-building-apis-forge-agents-speaker-summary.md) — short speaker briefing.

### Verify

```bash
curl http://localhost:8001/health   # {"status":"healthy","agent":"research-agent"}
curl http://localhost:8002/health   # {"status":"healthy","agent":"creator-agent"}
curl http://localhost:8003/health   # {"status":"healthy","agent":"podcaster-agent"}
curl http://localhost:8005/health   # {"status":"healthy","agent":"evaluator-agent"}
```

## Project Structure

```
azure.yaml                  # Azure Developer CLI (azd) configuration
Lab/
  docker-compose.yml         # Local multi-container setup
  docs/                      # Architecture docs & run-locally guide
  sample-output/             # Example blog and podcast transcript
  src/
    agent-research/          # Agent 1: Python, LangGraph, FastAPI
    agent-creator/           # Agent 2: .NET 10, Microsoft Agent Framework
    agent-podcaster/         # Agent 3: Python, GitHub Copilot SDK, TTS
    agent-evaluator/         # Agent 4: .NET 10, Microsoft Agent Framework, Dapr, Foundry Evals
    tts-server/              # GPU XTTS-v2 server (full mode only)
    dev-ui/                  # Static HTML + nginx (port 8080)
infra/
  main.bicep                 # Azure deployment (Foundry, ACR, ACA, Storage, GPU)
  main.parameters.json       # azd parameter wiring
  pre-rendered/              # Standalone Bicep for lab vendor provisioning
```

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Agent 1 — Research | Python 3.11+, LangGraph, LangChain, FastAPI, httpx, BeautifulSoup |
| Agent 2 — Creator | .NET 10, Microsoft Agent Framework, ASP.NET Minimal APIs |
| Agent 3 — Podcaster | Python 3.10+, GitHub Copilot SDK, pydub, httpx |
| Agent 4 — Evaluator | .NET 10, Microsoft Agent Framework, Dapr, Azure AI Foundry Evals API |
| TTS (lab mode) | Azure OpenAI `tts-1` |
| TTS (full mode) | Coqui XTTS-v2 on GPU, Azure OpenAI fallback |
| Protocol | A2A (Agent-to-Agent) |
| Infrastructure | Azure Container Apps, Azure Container Registry, Bicep |
| Observability | OpenTelemetry, Azure Application Insights |
| LLM | Azure OpenAI GPT-4o |
