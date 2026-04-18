# Stop Building APIs. Forge Agents with Azure — Guión a dos voces

> **Evento:** Global Azure 2026 · 18 abril 2026 · 12:50–13:35 · Track 2 – Sala 5  
> **Speakers:** Santiago Porras Rodríguez (🟦 SANTI) · Sergio Parra Guerra (🟩 SERGIO)  
> **Duración objetivo:** 25–30 minutos (dejando 5–10 min de preguntas)  
> **Repo:** `sparraguerra/globalazure-2026`

---

## Estructura general

| Bloque | Minutos | Quién abre | Contenido clave |
|--------|---------|------------|-----------------|
| 0 – Apertura y motivación | 0:00–3:00 | Santiago | Por qué ya no basta con APIs; qué son agentes |
| 1 – Arquitectura de la fábrica | 3:00–6:00 | Sergio | 4 agentes, A2A, Dapr, Aspire |
| 2 – .NET Aspire AppHost | 6:00–9:00 | Santiago | Código de `Program.cs` del AppHost |
| 3 – Research Agent | 9:00–13:00 | Sergio | LangGraph, herramientas, agent card A2A |
| 4 – Content Creator Agent | 13:00–17:00 | Santiago | MAF, workflow, executors, Dapr publish |
| 5 – Podcaster Agent | 17:00–20:00 | Sergio | Copilot SDK BYOK, TTS, audio pipeline |
| 6 – Evaluator Agent + Dapr | 20:00–23:00 | Santiago | Dapr subscribe, LLM-as-judge, Foundry Evals |
| 7 – Infra y observabilidad | 23:00–26:00 | Sergio | Bicep, ACA, OTEL → App Insights |
| 8 – Demo en vivo | 26:00–29:00 | Ambos | Lanzar flujo, ver trazas |
| 9 – Cierre | 29:00–30:00 | Ambos | Recap, call-to-action, QR al repo |

---

## Bloque 0 — Apertura y motivación (0:00–3:00)

🟦 **SANTI:** Buenas tardes. Soy Santiago Porras, desarrollador .NET, y este es Sergio Parra.

🟩 **SERGIO:** Hola a todos. Cloud engineer en Azure y co-autor de este experimento que vamos a destripar ahora.

🟦 **SANTI:** Levantad la mano los que tenéis una API REST desplegada en producción ahora mismo.  
*(pausa)*  
Vale, casi todos. Ahora otra pregunta: ¿cuántos de vosotros habéis tenido que añadir un endpoint nuevo esta semana solo para pasar datos de un servicio a otro?

🟩 **SERGIO:** Ese es el punto. Llevamos años construyendo aplicaciones pegando controllers, DTOs, serialización, versionado… y funciona. Pero cuando entran modelos de IA en la ecuación, el paradigma se queda corto. Un modelo de lenguaje no necesita un contrato Swagger; necesita **contexto**, **herramientas** y **autonomía** para decidir qué hacer.

🟦 **SANTI:** La sesión se titula *"Stop Building APIs. Forge Agents with Azure"*. No es que las APIs estén muertas, ojo. Pero hoy vamos a enseñaros que hay otra forma: construir **agentes autónomos** que se descubren entre sí, se comunican con un protocolo estándar y se despliegan como contenedores en Azure Container Apps.

🟩 **SERGIO:** Y no es PowerPoint. Es código. Vamos a recorrer paso a paso una aplicación real: una **fábrica de contenido** con cuatro agentes de IA que investigan, escriben, graban podcasts y evalúan la calidad… todo de forma autónoma.

🟦 **SANTI:** Cuatro tecnologías clave que veréis hoy:
1. **.NET Aspire** para componer la app
2. **A2A** (Agent-to-Agent) como protocolo de descubrimiento y comunicación
3. **Dapr** para pub/sub y componentes
4. **Azure AI Foundry** para los modelos y la evaluación

Empezamos.

---

## Bloque 1 — Arquitectura de la fábrica (3:00–6:00)

> 📂 **Ficheros de este bloque:** `Lab/docs/architecture.md`

🟩 **SERGIO:** *(abrir `Lab/docs/architecture.md` en el preview de VS Code o como imagen en pantalla)*

Esto es lo que hemos construido. Un usuario escribe un tema — por ejemplo, *"Azure Container Apps for developers"* — en una interfaz web. Y pasan cuatro cosas:

1. El **Research Agent** — Python, LangGraph — sale a buscar en Microsoft Learn, Azure Blog, Tech Community, GitHub… Ranquea, filtra, descarga contenido real, y devuelve un **research brief** estructurado.
2. El **Content Creator** — .NET 10, Microsoft Agent Framework — toma ese brief y genera un blog post, un post de LinkedIn y un hilo de Twitter. Todo con fuentes reales.
3. El **Podcaster** — Python, GitHub Copilot SDK — convierte eso en un guión de podcast conversacional a dos voces, lo pasa por text-to-speech y ensambla el audio final.
4. El **Evaluator** — .NET 10, MAF — recibe un evento vía Dapr pub/sub cuando el Creator termina, y evalúa la calidad con un LLM-as-judge y con Azure AI Foundry Evals.

🟦 **SANTI:** Fijaos que los agentes 1, 2 y 3 se comunican con **A2A**: cada uno expone un `/.well-known/agent.json` y un endpoint `/a2a` JSON-RPC. El agente 4, en cambio, se activa por **Dapr pub/sub** — un patrón event-driven. Es decir, tenemos dos patrones de comunicación entre agentes en la misma app: request/response con A2A y fire-and-forget con Dapr.

🟩 **SERGIO:** Y todo esto se compone con **.NET Aspire** en local y se despliega con `azd up` a **Azure Container Apps**. Cada agente es un contenedor independiente. Vamos al código.

---

## Bloque 2 — .NET Aspire AppHost (6:00–9:00)

> 📂 **Ficheros de este bloque:** `Lab/src/aspire/ContentAgentFactory.AppHost/Program.cs`

🟦 **SANTI:** *(abrir `Lab/src/aspire/ContentAgentFactory.AppHost/Program.cs`)*

Este es el punto de entrada de toda la aplicación. Aspire nos da una forma declarativa de definir todos los servicios, sus puertos, sus variables de entorno y sus sidecars.

Mirad: primero cargamos las variables de un fichero `.env` para desarrollo local:

```csharp
var labEnvFile = Path.GetFullPath(
    Path.Combine(builder.AppHostDirectory, "..", "..", "..", ".env"));
var dotEnvVars = LoadDotEnv(labEnvFile);
```

🟩 **SERGIO:** La clave está en la prioridad: `.env` es la base, pero user-secrets y la config de Aspire pueden sobreescribirla. Y las variables que Aspire gestiona — OTEL endpoints, puertos — nunca se machacan. Esto es importante para no romper la telemetría cuando alguien mete su `AZURE_OPENAI_KEY` en el `.env`.

🟦 **SANTI:** Ahora los servicios. Fijaos en cómo se declaran. Primero el TTS server, que es un contenedor con GPU:

```csharp
var ttsServer = builder.AddContainer("tts-server", "tts-server")
    .WithDockerfile("../../tts-server")
    .WithHttpEndpoint(port: 8004, name: "http-tts-server", isProxied: false);
```

Después los agentes Python — `AddPythonApp` — con inyección de env y OTEL exporter:

```csharp
var researchAgent = builder.AddPythonApp(
        "agent-research", "../../agent-research", "run.py")
    .WithHttpEndpoint(port: 8001, ...)
    .WithEnvironment(injectServiceEnv)
    .WithOtlpExporter();
```

🟩 **SERGIO:** Y ahora viene lo interesante: los agentes .NET llevan un **sidecar de Dapr**.

```csharp
var daprComponentsDir = Path.GetFullPath(
    Path.Combine(builder.AppHostDirectory, "..", "..", "..", "dapr", "components"));

builder.AddProject<Projects.AgentCreator>("agent-creator")
    .WithHttpEndpoint(port: 8002, ...)
    .WithDaprSidecar(new DaprSidecarOptions
    {
        AppId = "agent-creator",
        AppPort = 8002,
        ResourcesPaths = [daprComponentsDir]
    });
```

🟦 **SANTI:** Lo mismo para el Evaluator en el puerto 8005. Aspire levanta los Dapr sidecars automáticamente apuntando a la carpeta `Lab/dapr/components/` donde está el componente de pub/sub. Veremos eso dentro de un momento.

🟩 **SERGIO:** Y al final, la Dev UI: un contenedor nginx estático en el 8080 que habla directamente a los agentes por sus puertos. Sin API gateway, sin reverse proxy. Cada agente es un ciudadano de primera clase.

---

## Bloque 3 — Research Agent (9:00–13:00)

> 📂 **Ficheros de este bloque:** `Lab/src/agent-research/agent.py` · `Lab/src/agent-research/a2a.py`

🟩 **SERGIO:** *(abrir `Lab/src/agent-research/agent.py`)*

El Research Agent es el primero de la cadena. Está hecho con **LangGraph**, que nos da un grafo de estados para orquestar la investigación. Mirad el estado:

```python
class ResearchState(TypedDict):
    topic: str
    docs: list[dict]           # Microsoft Learn
    repos: list[dict]          # GitHub Azure-Samples
    blogs: list[dict]          # Azure Blog + Tech Community
    updates: list[dict]        # Azure Updates
    ranked_urls: list[str]     # URLs priorizadas por el LLM
    fetched_content: list[dict] # Contenido real descargado
    brief: dict | None
```

🟦 **SANTI:** O sea que no es un simple "llama al LLM y dame un resumen". Hay búsqueda real en cinco fuentes, ranking con IA, fetch de contenido real de las URLs top, extracción de enlaces de profundidad 1… y al final una síntesis con citas reales.

🟩 **SERGIO:** Cada paso del grafo tiene su span de OpenTelemetry manual:

```python
with _tracer.start_as_current_span(
    "execute_tool search_docs",
    kind=SpanKind.INTERNAL,
    attributes={
        "gen_ai.operation.name": "execute_tool",
        "gen_ai.tool.name": "search_docs",
    },
):
    state["docs"] = await search_learn(state["topic"], top=40)
```

Esto es clave para después ver en App Insights exactamente cuánto tardó cada herramienta.

🟦 **SANTI:** ¿Y cómo se expone este agente al mundo? A2A.

🟩 **SERGIO:** *(abrir `Lab/src/agent-research/a2a.py`)*

Exacto. El agente publica una **agent card** en `/.well-known/agent.json`:

```python
card = AgentCard(
    name="research-agent",
    description="Deep-dive research on technology topics...",
    url=f"{url}/a2a",
    version="1.0.0",
    protocolVersion="0.3.0",
    capabilities=AgentCapabilities(streaming=False),
    skills=[AgentSkill(
        id="research-topic",
        name="Research a Technology Topic",
        ...
    )]
)
```

Cualquier otro agente — o incluso una herramienta externa — puede descubrir qué sabe hacer este agente con un simple GET a esa URL. Y para ejecutarlo, un POST JSON-RPC a `/a2a`.

🟦 **SANTI:** Esto es lo que proponemos como alternativa al "endpoint REST hardcodeado". El agente se autodescribe, dice qué sabe hacer, qué formatos acepta… y el caller decide si lo usa o no. Es descubrimiento dinámico.

---

## Bloque 4 — Content Creator Agent (13:00–17:00)

> 📂 **Ficheros de este bloque:** `Lab/src/agent-creator/AgentCreator/ContentFactoryWorkflow.cs` · `Lab/src/agent-creator/AgentCreator/ContentCreatorAgent.cs` · `Lab/src/agent-creator/AgentCreator/Executors/BlogGenerationExecutor.cs` · `Lab/src/agent-creator/AgentCreator/Models/ContentCreatedMessage.cs`

🟦 **SANTI:** *(abrir `Lab/src/agent-creator/AgentCreator/ContentFactoryWorkflow.cs`)*

Ahora el Content Creator. Aquí entramos en **.NET 10** y el **Microsoft Agent Framework**. Este agente tiene un patrón de workflow con executors encadenados:

```
BriefInput → BlogGeneration → SocialGeneration → Output
```

Cada executor es un paso del pipeline con su propia llamada al LLM, su propio span de OTel, y su propia responsabilidad.

🟩 **SERGIO:** Mirad cómo se construye el grafo con el `WorkflowBuilder`:

```csharp
var workflowBuilder = new WorkflowBuilder(briefInput)
    .AddEdge(briefInput, blogGeneration)
    .AddEdge(blogGeneration, socialGeneration)
    .AddEdge(socialGeneration, output)
    .WithOutputFrom(output);
```

Es un DAG de executors. El `BriefInputExecutor` parsea el brief de Research, el `BlogGenerationExecutor` genera el markdown del blog, el `SocialGenerationExecutor` crea LinkedIn y tweets, y el `OutputExecutor` empaqueta todo.

🟦 **SANTI:** *(abrir `Lab/src/agent-creator/AgentCreator/ContentCreatorAgent.cs`)* El `ContentCreatorAgent` inicializa el `IChatClient` de Azure OpenAI con instrumentación OTel:

```csharp
ChatClient = aoaiClient.GetChatClient(deployment)
    .AsIChatClient()
    .AsBuilder()
    .UseOpenTelemetry(sourceName: "creator-agent",
        configure: c => c.EnableSensitiveData = true)
    .Build();
```

🟩 **SERGIO:** Eso de `EnableSensitiveData = true` significa que en las trazas de OTel veremos los prompts y las respuestas del LLM. Muy útil para depurar en desarrollo.

🟦 **SANTI:** *(volver a `ContentFactoryWorkflow.cs`, método `PublishContentCreatedAsync`)* Ahora la parte de Dapr. Cuando el workflow termina, publica un evento al topic `content-created`:

```csharp
await _daprClient.PublishEventAsync("pubsub", "content-created", message);
```

Ese `message` lleva el blog en markdown, el post de LinkedIn, los tweets, word count, fuentes usadas… Todo lo que el Evaluator necesita para juzgar la calidad.

🟩 **SERGIO:** Y fijaos: el publish está en un try/catch y es *non-fatal*. Si Dapr no está disponible, el flujo principal no falla. La evaluación es un side-effect asíncrono, no está en la ruta crítica.

---

## Bloque 5 — Podcaster Agent (17:00–20:00)

> 📂 **Ficheros de este bloque:** `Lab/src/agent-podcaster/script_generator.py` · `Lab/src/agent-podcaster/agent.py`

🟩 **SERGIO:** *(abrir `Lab/src/agent-podcaster/script_generator.py`)*

El Podcaster es especial porque usa el **GitHub Copilot SDK** con patrón BYOK — *Bring Your Own Key*. Esto nos permite usar Azure OpenAI como provider pero con las abstracciones del SDK de Copilot.

```python
def _get_azure_provider() -> dict:
    return {
        "type": "azure",
        "base_url": os.environ["AZURE_OPENAI_ENDPOINT"],
        "api_key": os.environ["AZURE_OPENAI_API_KEY"],
        "azure": {
            "api_version": "2024-12-01-preview",
        },
    }
```

🟦 **SANTI:** ¿Y por qué no usar directamente el SDK de Azure OpenAI?

🟩 **SERGIO:** Porque el Copilot SDK nos da herramientas declarativas con `@define_tool`. El LLM puede decidir por sí solo si necesita enriquecer fuentes durante la generación del guión:

```python
@define_tool(description="Fetch a web page and extract its readable text content")
async def fetch_url_tool(params: FetchUrlParams) -> dict:
    result = await fetch_page_content(params.url)
    return {"url": result["url"], "content": result.get("content", "")[:3000]}
```

🟦 **SANTI:** Ajá, o sea que el LLM está generando el guión del podcast y si necesita más detalle de una fuente, llama a la herramienta él solo.

🟩 **SERGIO:** *(abrir `Lab/src/agent-podcaster/agent.py`)*

Exacto. Y después del guión viene el pipeline de audio: TTS con voz clonada o con Azure OpenAI TTS, ensamblaje de segmentos alternando host e invitado, y subida a Blob Storage. Todo con spans de OTel.

```python
span = _tracer.start_span(
    "invoke_agent podcaster-agent",
    kind=SpanKind.INTERNAL,
    attributes={
        "gen_ai.operation.name": "invoke_agent",
        "gen_ai.agent.name": "podcaster-agent",
    },
)
```

🟦 **SANTI:** Tres agentes, tres lenguajes de programación distintos (bueno, Python dos veces y .NET), tres frameworks diferentes… pero todos hablan A2A y todos emiten telemetría al mismo colector.

---

## Bloque 6 — Evaluator Agent + Dapr (20:00–23:00)

> 📂 **Ficheros de este bloque:** `Lab/src/agent-evaluator/AgentEvaluator/Program.cs` · `Lab/src/agent-evaluator/AgentEvaluator/ContentEvaluationService.cs` · `Lab/src/agent-evaluator/AgentEvaluator/FoundryEvaluationService.cs` · `Lab/dapr/components/pubsub.yaml`

🟦 **SANTI:** *(abrir `Lab/src/agent-evaluator/AgentEvaluator/Program.cs`)*

El cuarto agente es el Evaluator. Y aquí es donde Dapr brilla. Recordad que el Content Creator publica al topic `content-created`. El Evaluator se suscribe:

```csharp
app.UseCloudEvents();

app.MapPost("/content-created", async (
    ContentCreatedMessage message,
    ContentEvaluationService evaluationService,
    FoundryEvaluationService foundryService,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    // 1. LLM-as-judge evaluation (local, fast)
    var localResult = await evaluationService.EvaluateAsync(message, cancellationToken);

    // 2. Foundry cloud evaluation (async)
    FoundryEvaluationReport? foundryReport = null;
    if (foundryService.IsAvailable)
        foundryReport = await foundryService.SubmitAsync(message, cancellationToken);

    return Results.Ok(result);
}).WithTopic("pubsub", "content-created");

app.MapSubscribeHandler();
```

🟩 **SERGIO:** Desempaquetemos esto. Dapr llama a `GET /_dapr/subscribe` al arrancar. Descubre que el Evaluator está suscrito al topic `content-created` del componente `pubsub`. Cuando llega un CloudEvent, Dapr lo reenvía al POST `/content-created`. Nosotros solo escribimos un handler de ASP.NET normal.

🟦 **SANTI:** Y el handler hace dos cosas:

**Primero**, un **LLM-as-judge** local.

*(abrir `Lab/src/agent-evaluator/AgentEvaluator/ContentEvaluationService.cs`)*

Usa MAF (`ChatClientAgent`) con un system prompt de evaluador experto. Le pasa el blog, los tweets, LinkedIn… y le pide scores numéricos de 1 a 10 en calidad del blog, calidad social y relevancia:

```csharp
_agent = new ChatClientAgent(
    evaluatorAgent.ChatClient,
    name: "evaluator-agent",
    instructions: "You are an expert content quality evaluator..."
);
```

🟩 **SERGIO:** **Segundo**, si Foundry está configurado, envía una evaluación cloud al **Azure AI Foundry Evals API**.

*(abrir `Lab/src/agent-evaluator/AgentEvaluator/FoundryEvaluationService.cs`)*

Usa el `EvaluationClient` del SDK de Azure OpenAI para crear un eval run con evaluadores built-in: `coherence` y `fluency`. Los resultados aparecen directamente en el portal de Foundry.

```csharp
private static readonly string[] BuiltInEvaluators =
    ["score_model:coherence", "score_model:fluency"];
```

🟦 **SANTI:** Pensad en lo que acabamos de ver: el Creator publica, el Evaluator reacciona. No hay acoplamiento directo. Puedes desplegar el Evaluator o quitarlo y el resto sigue funcionando. Eso es event-driven.

🟩 **SERGIO:** ¿Y el componente de Dapr? Veámoslo.

*(abrir `Lab/dapr/components/pubsub.yaml`)*

```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: pubsub
spec:
  type: pubsub.redis
  version: v1
  metadata:
    - name: redisHost
      value: localhost:6379
```

En local usa Redis. En Azure Container Apps, cambias este componente por Azure Service Bus o el pub/sub nativo de Dapr en ACA y no tocas ni una línea de código.

---

## Bloque 7 — Infraestructura y observabilidad (23:00–26:00)

> 📂 **Ficheros de este bloque:** `infra/main.bicep` · `azure.yaml` · `Lab/docs/otel-architecture.md`

🟩 **SERGIO:** *(abrir `infra/main.bicep`)*

El despliegue a Azure se hace con **Bicep** y **azd**. Un `azd up` y tienes todo: ACR, Container Apps Environment, AI Services, Storage Account, Log Analytics, Application Insights.

🟦 **SANTI:** Lo más interesante del Bicep es cómo se configura la observabilidad. El ACA Environment tiene un managed OpenTelemetry collector:

En cada servicio, el SDK de OTel — sea Python con `opentelemetry-sdk` o .NET con `OpenTelemetry.Extensions.Hosting` — envía trazas, métricas y logs al colector gestionado. Y este las reenvía a Application Insights.

🟩 **SERGIO:** En la app hemos visto cómo cada agente instrumenta sus spans: el Research Agent con `_tracer.start_as_current_span(...)`, el Creator con `UseOpenTelemetry(sourceName: "creator-agent")`, el Podcaster igual… Todos esos spans acaban en la misma instancia de App Insights. Eso significa que una petición que entra por la Dev UI, pasa por Research, Creator, Podcaster y Evaluator se ve como una **traza distribuida end-to-end**.

🟦 **SANTI:** Y en Foundry, las trazas de los modelos se conectan con los eval runs. Puedes ver: "esta petición generó este blog, y el Evaluator le dio un 8.2 en calidad". Todo trazable.

🟩 **SERGIO:** *(abrir `azure.yaml`)* Y como usamos `azure.yaml` con definiciones de servicio, cada agente se construye como imagen Docker, se sube a ACR y se despliega como Container App. Sin Kubernetes, sin Helm charts.

---

## Bloque 8 — Demo en vivo (26:00–29:00)

> 🖥️ **Para este bloque:** terminal con `dotnet run --project Lab/src/aspire/ContentAgentFactory.AppHost` (o `docker compose up` en `Lab/`) · navegador en `http://localhost:8080` · portal de App Insights abierto en otra pestaña

🟦 **SANTI:** Vamos a verlo funcionando. *(cambiar al terminal)*

🟩 **SERGIO:** *(lanzar la aplicación con Aspire o Docker Compose)*

Abrimos la Dev UI en `localhost:8080`. Metemos un tema: *"Azure Container Apps dynamic sessions"*.

🟦 **SANTI:** *(narrar mientras se ejecuta)*

Primero vemos que Research Agent arranca: está buscando en Learn, en GitHub… Tarda unos segundos porque está haciendo fetch real de las páginas.

🟩 **SERGIO:** Ahora el Creator recibe el brief y empieza el workflow: `BriefInput` → `BlogGeneration` → `SocialGeneration` → `Output`. Y al terminar… publica el evento a Dapr.

🟦 **SANTI:** *(señalar logs o UI)* ¡Ahí! El Evaluator ha recibido el `content-created` event. Está evaluando… Blog quality: 8.1, Social quality: 7.5, Relevance: 8.8. Y si miramos Foundry… el eval run ya está registrado.

🟩 **SERGIO:** Mientras tanto, el Podcaster está generando el guión y pasándolo por TTS. *(esperar al resultado de audio)*

🟦 **SANTI:** Vamos a ver las trazas en App Insights. *(abrir portal / dashboard)*

Aquí está la traza distribuida completa. Podemos ver cada span: `search_docs`, `rank_urls`, `fetch_content`, `workflow content-factory-pipeline`, `invoke_agent podcaster-agent`… Todo correlacionado con un mismo trace ID.

🟩 **SERGIO:** Y si hacemos drill-down en los spans del LLM, vemos los prompts, los tokens consumidos, la latencia de cada llamada. Eso es lo que da `EnableSensitiveData = true` en OTel.

---

## Bloque 9 — Cierre (29:00–30:00)

🟦 **SANTI:** Recapitulemos. Hoy habéis visto:

- **Cuatro agentes autónomos** — cada uno con su lenguaje, su framework, su responsabilidad. Sin monolito.
- **A2A** como protocolo estándar para que los agentes se descubran y se hablen.
- **Dapr pub/sub** para comunicación event-driven entre servicios, con un componente que se cambia sin tocar código.
- **.NET Aspire** para componer todo en un AppHost declarativo.
- **Azure AI Foundry** para modelos, evaluación y gobernanza.
- **OpenTelemetry end-to-end** desde cada agente hasta App Insights.

🟩 **SERGIO:** La idea central es esta: **no construyas más endpoints, construye agentes**. Agentes que se autodescriben, que tienen herramientas propias, que se comunican con un protocolo estándar, y que puedes desplegar, escalar y observar de forma independiente en Azure Container Apps.

🟦 **SANTI:** Todo el código está en el repo. *(mostrar QR)*

```
github.com/sparraguerra/globalazure-2026
```

Ahí tenéis el lab manual paso a paso, la documentación de arquitectura de cada agente, y los ficheros de infraestructura para desplegarlo vosotros.

🟩 **SERGIO:** Tenemos unos minutos para preguntas. ¿Quién se anima?

🟦 **SANTI:** Gracias a todos.

---

## Notas para los speakers

### Qué tener abierto antes de empezar
1. **VS Code** con el workspace del repo abierto
2. Ficheros pre-abiertos en tabs:
   - `Lab/src/aspire/ContentAgentFactory.AppHost/Program.cs`
   - `Lab/src/agent-research/agent.py`
   - `Lab/src/agent-research/a2a.py`
   - `Lab/src/agent-creator/AgentCreator/ContentFactoryWorkflow.cs`
   - `Lab/src/agent-creator/AgentCreator/ContentCreatorAgent.cs`
   - `Lab/src/agent-podcaster/script_generator.py`
   - `Lab/src/agent-podcaster/agent.py`
   - `Lab/src/agent-evaluator/AgentEvaluator/Program.cs`
   - `Lab/src/agent-evaluator/AgentEvaluator/ContentEvaluationService.cs`
   - `Lab/src/agent-evaluator/AgentEvaluator/FoundryEvaluationService.cs`
   - `Lab/dapr/components/pubsub.yaml`
3. **Diagrama de arquitectura** (`Lab/docs/architecture.md`) renderizado o como imagen
4. **Terminal** con el AppHost o Docker Compose listo para lanzar
5. **Browser** con la Dev UI en `localhost:8080` y App Insights abierto
6. **QR** al repo preparado en la última slide

### Reparto de roles
- **Santiago** lidera los bloques de .NET: Aspire, Content Creator, Evaluator, demo narración
- **Sergio** lidera los bloques de Python: Research Agent, Podcaster, infraestructura
- En la demo ambos participan: Sergio lanza, Santiago narra
- Las transiciones son diálogos naturales (pregunta → respuesta)

### Gestión del tiempo
- Si la demo falla: hay outputs pregenerados en `Lab/sample-output/` (blog.md, podcast_transcript.md, evals.jsonl)
- Si vais rápido: extended la demo mostrando más trazas o haciendo una segunda petición
- Si vais lentos: recortad el bloque 7 (infra) resumiendo en 1 minuto y pasad a la demo

### Preguntas frecuentes preparadas
1. **¿Por qué no usar un orquestador central?** → Queremos agentes independientes que se puedan desplegar/escalar por separado. El "orquestador" es simplemente el flujo A2A encadenado.
2. **¿A2A es un estándar real?** → Es una propuesta de Google, versión 0.3.0, con adopción creciente. Compatible con agent cards de descubrimiento y JSON-RPC.
3. **¿Por qué Dapr solo para el Evaluator?** → El Evaluator es event-driven: no necesita respuesta síncrona. A2A es mejor para request/response. Usamos cada patrón donde encaja.
4. **¿Funciona sin Azure OpenAI?** → Hay fallbacks template-based en el Creator y scores por defecto en el Evaluator. La app arranca y funciona sin LLM, solo con menos calidad.
5. **¿Cuánto cuesta esto en Azure?** → ACA consumption plan es pay-per-use. El coste real viene del modelo GPT-4o. Para un lab, unos pocos euros/día.
