# Speaker Summary — Stop Building APIs. Forge Agents with Azure

## Mensaje principal

Una fábrica de contenido con **4 agentes autónomos** que investigan, escriben, graban podcasts y evalúan calidad. Dos patrones de comunicación: **A2A** (request/response) y **Dapr pub/sub** (event-driven). Todo compuesto con .NET Aspire y desplegado en Azure Container Apps.

## Qué enseñar (en orden del guión)

1. **Aspire AppHost** — composición declarativa de 4 agentes + sidecars Dapr (`Program.cs`).
2. **Research Agent** — Python, LangGraph, búsqueda en 5 fuentes, fetch real, agent card A2A.
3. **Content Creator** — .NET 10, MAF workflows con 4 executors encadenados, Dapr publish al terminar.
4. **Podcaster** — GitHub Copilot SDK BYOK, herramientas declarativas, TTS, audio pipeline.
5. **Evaluator** — Dapr subscribe `content-created`, LLM-as-judge local + Foundry Evals cloud.
6. **Observabilidad** — OTel en todos los agentes → App Insights → traza distribuida end-to-end.
7. **Demo en vivo** — lanzar topic, ver flujo completo, mostrar trazas y eval scores.

## 4 tecnologías clave

| Tech | Rol en el proyecto |
|------|-------------------|
| **.NET Aspire** | Componer la app: servicios, puertos, env, sidecars Dapr |
| **A2A** | Protocolo de descubrimiento (`agent.json`) y ejecución (`/a2a` JSON-RPC) |
| **Dapr** | Pub/sub entre Creator → Evaluator (`content-created` topic, Redis local / Service Bus en Azure) |
| **Azure AI Foundry** | Modelos GPT-4o + TTS, Evals API (coherence, fluency), portal de resultados |

## Idea fuerza

La abstracción principal ya no es el endpoint, sino el agente con capacidades, herramientas y trazabilidad. A2A para comunicación síncrona, Dapr para eventos asíncronos.

## Encaje con la descripción de la sesión

- *"sustituye endpoints por agentes autónomos"* → 4 agentes con agent cards, sin controllers REST.
- *"Aspire para componer la app"* → AppHost con `AddPythonApp`, `AddProject`, `WithDaprSidecar`.
- *"Dapr para comunicación y componentes"* → pub/sub Redis (`pubsub.yaml`), sidecars en Creator y Evaluator.
- *"Foundry Agent Service para diseñar, desplegar y gobernar"* → Evals API, portal de evaluación.
- *"código, despliegue y telemetría"* → Code walkthrough + `azd up` + OTel → App Insights.

## Plan B

- Si la demo falla: `Lab/sample-output/` tiene blog.md, podcast_transcript.md, evals.jsonl.
- Si vais rápido: segunda petición o drill-down en trazas.
- Si vais lentos: recortar bloque de infra a 1 minuto.

## Frase de cierre

No dejamos de usar HTTP; dejamos de diseñar la solución como si HTTP y CRUD fueran la unidad principal. Aquí la unidad es el agente. Y Dapr, A2A y Aspire son el pegamento que hace que funcionen juntos.