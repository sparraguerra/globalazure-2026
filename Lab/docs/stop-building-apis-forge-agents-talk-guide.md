# Stop Building APIs. Forge Agents with Azure

## Qué es este proyecto

Este repositorio implementa una factoría de contenido multiagente desplegable en Azure. A partir de un tema técnico, la solución investiga fuentes reales, genera contenido editorial y, opcionalmente, crea un podcast con audio sintetizado.

La idea central no es exponer un único endpoint que haga todo, sino orquestar varias capacidades autónomas especializadas que colaboran entre sí:

1. Un agente investiga y construye un brief fiable.
2. Otro agente transforma ese brief en piezas de contenido.
3. Un tercer agente convierte ese resultado en un formato conversacional y audio.
4. Un cuarto agente evalúa la calidad del contenido generado e integra feedback con Foundry.

El proyecto sirve como demo de una aplicación AI-native que combina agentes, modelos, telemetría y despliegue cloud sin esconder los detalles de implementación.

## Qué hace exactamente

El flujo funcional es este:

1. El usuario escribe un tema en la Dev UI.
2. El agente de research busca en Microsoft Learn, Azure Blog, Tech Community, Azure Updates y GitHub.
3. Ese agente prioriza resultados con un LLM, descarga contenido relevante y sintetiza un brief estructurado.
4. El agente creador convierte ese brief en blog post, post de LinkedIn y social thread.
5. El agente podcaster, si está habilitado, reusa el brief para producir un guion conversacional y generar audio.
6. El agente evaluator se dispara automáticamente (vía Dapr pub/sub) cuando el contenido está listo, ejecuta evaluaciones LLM-as-judge y registra runs en Azure AI Foundry.
7. La solución muestra resultados, conserva trazas y permite revisar evaluaciones en Microsoft Foundry.

El valor de demo está en que cada etapa produce un artefacto distinto y verificable: fuentes, brief, contenido, guion, audio y telemetría.

## Cómo funciona por dentro

## Arquitectura general

La arquitectura es principalmente lineal para los tres primeros agentes, pero con una rama asíncrona para evaluación:

`Dev UI -> Research Agent -> Content Creator Agent -> Podcaster Agent`
                                        |
                                        v (Dapr pub/sub)
                                   Evaluator Agent

Cada agente se ejecuta en su propio contenedor y expone dos superficies:

1. Un agent card en `/.well-known/agent.json` para descubrimiento.
2. Un endpoint `/a2a` basado en JSON-RPC para recibir tareas (Research, Content Creator, Podcaster).
3. El Evaluator escucha eventos Dapr pub/sub en el tema `content-created`.

Eso hace que la unidad de integración no sea un contrato REST tradicional centrado en recursos, sino un agente con capacidades declaradas y una interfaz orientada a tareas. El Evaluator demuestra cómo integrar patrones de pub/sub para desacoplar evaluación del flujo principal.

## Agente 1: Research

Está implementado en Python con LangGraph y FastAPI. Su trabajo no es generar texto final, sino reducir incertidumbre.

Responsabilidades principales:

1. Descomponer y entender el tema y la audiencia.
2. Buscar en varias fuentes oficiales y semioficiales.
3. Ranquear resultados con LLM.
4. Descargar contenido y seguir enlaces de primer nivel sobre dominios confiables.
5. Crear un brief estructurado para el siguiente agente.

Esto es importante para la charla porque permite explicar una idea clave: en sistemas agentic, el primer paso no es “responder”, sino “reunir contexto útil y trazable”.

## Agente 2: Content Creator

Está implementado en .NET 10 con Microsoft Agent Framework y ASP.NET Minimal API. Recibe el brief y ejecuta un workflow interno con ejecutores especializados.

Responsabilidades principales:

1. Validar el brief recibido.
2. Generar un artículo técnico original.
3. Derivar contenido social a partir del artículo.
4. Mantener estado de workflow y spans de observabilidad consistentes.

Aquí tienes el mejor puente con el mensaje de la sesión: no se trata de un backend que renderiza JSON, sino de un proceso que encadena intención, estado, razonamiento y salida multi-formato.

## Agente 3: Podcaster

Está implementado en Python con FastAPI y GitHub Copilot SDK en modo BYOK contra Azure OpenAI. Convierte el brief en un podcast a dos voces y genera audio.

Responsabilidades principales:

1. Re-enriquecer fuentes para el guion.
2. Generar conversación multi-turno.
3. Autoevaluar y refinar el guion.
4. Ajustar pronunciaciones técnicas.
5. Sintetizar audio con Azure OpenAI TTS o con un servidor XTTS-v2 sobre GPU.
6. Montar MP3 y subirlo a Blob Storage.

Para la charla, este agente demuestra muy bien que un agente no tiene por qué terminar en texto. Puede terminar en un artefacto multimodal.

## Infraestructura y despliegue

La infraestructura se define en Bicep y se despliega con `azd`. El objetivo es que el paso de local a cloud sea demostrable.

La solución crea, entre otros:

1. Azure Container Apps Environment.
2. Azure Container Registry.
3. Azure AI Services / Foundry con despliegues de modelo.
4. Application Insights y Log Analytics.
5. Storage Account para audios.
6. En modo `full`, perfiles GPU y almacenamiento adicional para XTTS.

La parte local se puede correr con Docker Compose o con .NET Aspire AppHost. Aspire encaja como orquestador de desarrollo local: levanta servicios, inyecta variables y enruta telemetría OTLP.

## Observabilidad

La telemetría es una parte fuerte del proyecto. Los agentes emiten trazas y logs con OpenTelemetry y Azure Container Apps enruta esas señales a Application Insights.

Esto te permite enseñar algo que en muchas demos se omite:

1. Qué llamada hizo cada agente.
2. Qué dependencias externas tocó.
3. Cómo encadenó pasos de pipeline.
4. Cómo observar comportamiento y no solo “ver que funciona”.

Además, el repo está preparado para registrar agentes y revisar trazas/evaluaciones en Microsoft Foundry.

## Cómo encaja con tu charla

Tu abstract habla de cuatro ideas: app AI-native, Azure Container Apps, Aspire/Dapr y Foundry con observabilidad. Este proyecto encaja muy bien con tres de ellas y parcialmente con una cuarta.

## 1. Sustituir endpoints por agentes autónomos

Este repo encaja de forma directa con el título de la charla.

La aplicación no gira alrededor de un CRUD ni de un conjunto de endpoints REST de negocio. Gira alrededor de capacidades agentic coordinadas: investigar, redactar, narrar y sintetizar.

La mejor forma de contarlo es esta:

“Seguimos teniendo HTTP, pero la abstracción importante ya no es el endpoint. La abstracción importante es el agente, su contrato de tareas, su estado, sus herramientas y su trazabilidad.”

## 2. Azure Container Apps como runtime de agentes

También encaja de forma directa.

Cada agente es un contenedor independiente, lo que permite:

1. Escalado y despliegue desacoplado.
2. Diferentes stacks por agente.
3. Aislar capacidades pesadas como TTS sobre GPU.
4. Un camino claro de local a cloud.

Este punto es muy demostrable en vivo porque se ve el mapeo entre repo, contenedores y recursos de Azure.

## 3. Foundry como gobierno, registro y evaluación

Encaja bien si lo presentas como plano de control y de operación, no como el único sitio donde “viven” los agentes.

Este proyecto usa Foundry para:

1. Proveer modelos.
2. Registrar activos/agentes.
3. Revisar trazas.
4. Ejecutar evaluaciones sobre datasets.

Eso encaja muy bien con la narrativa de “del prototipo local a un entorno operable”.

## 4. Aspire y Dapr

Ahora sí puedes hablar de Dapr con confianza, porque aparece de forma visible en el repo.

Aspire sí aparece en el repo como experiencia de orquestación local mediante el AppHost de .NET Aspire. Te sirve para mostrar composición local, variables de entorno compartidas y telemetría.

Dapr también aparece de forma operativa: los agentes usan Dapr para pub/sub, específicamente en el flujo donde el Content Creator publica eventos `content-created` y el Evaluator se suscribe a esos eventos. Es el mecanismo que desacopla la evaluación asíncrona del pipeline principal.

En local, Dapr se levanta como sidecar. En Azure Container Apps, el sidecar Dapr está inyectado automáticamente por la plataforma. El componente `pubsub.yaml` en `Lab/dapr/components/` define el tipo de pub/sub que usas (en el repo, Redis o similar según configuración).

Esto cierra muy bien el mensaje: Aspire + Dapr + Container Apps crean un camino clara y progresiva desde local (composición con Aspire) a cloud (sidecars Dapr, escalado con Container Apps).

## Guion recomendado para contarlo en directo

## Versión actualizada del guion

Este guion está pensado para una charla técnica con demo en vivo. No intenta sonar a texto memorizado; intenta darte frases de arranque, transición y cierre para que puedas conducir la sesión sin perder el hilo.

## Apertura: 2 minutos

“Muchas aplicaciones con IA siguen construyéndose como si todo fuese un endpoint más. Metemos prompt, herramientas y algo de orquestación detrás de una API, y esperamos que eso escale. El problema es que, cuando el flujo se vuelve más rico, esa API acaba concentrando demasiadas responsabilidades.”

“Lo que os quiero enseñar hoy es otro enfoque. En vez de pensar en un backend único que hace de todo, vamos a pensar en agentes especializados. Cada agente tiene un objetivo, un contrato, herramientas concretas, telemetría y un despliegue independiente.”

“La demo es esta: le damos un tema técnico a la aplicación, un agente investiga fuentes reales, otro convierte ese material en contenido y un tercero lo transforma en un podcast con audio. Todo esto corriendo en Azure Container Apps, con observabilidad en OpenTelemetry y Azure Monitor, y con Microsoft Foundry como pieza de modelos, registro y evaluación.”

## Problema y tesis: 2 minutos

“La tesis de la charla no es que las APIs desaparezcan. Seguimos usando HTTP. Lo que cambia es la unidad de diseño. Dejamos de diseñar alrededor de endpoints y empezamos a diseñar alrededor de capacidades agentic.”

“Cuando el problema requiere investigar, tomar contexto, transformar salidas y producir artefactos diferentes, un único endpoint empieza a ser una mala abstracción. En cambio, separar ese trabajo entre agentes hace más visible el flujo, más fácil de operar y bastante más fácil de evolucionar.”

## Enseñando la arquitectura: 3 minutos

> 📂 **Ficheros de este bloque:** `Lab/docs/architecture.md`

“La arquitectura de esta demo es muy simple de entender. Tenemos una Dev UI y cuatro agentes. El primero investiga. El segundo escribe. El tercero genera un podcast. El cuarto evalúa la calidad. Lo interesante es que por debajo no estamos mezclando todo en un monolito, sino desplegando cada capacidad como un contenedor independiente.”

“Eso nos permite combinar stacks distintos sin problema: Python para research con LangGraph, .NET para content creation con Microsoft Agent Framework, Python otra vez para la parte multimodal del podcaster, y .NET también para el evaluator.”

"La comunicación tiene dos patrones. Los tres primeros agentes se orquestan con A2A sobre HTTP y JSON-RPC. Pero el cuarto agente, el evaluator, entra en juego de forma asíncrona: escucha eventos de Dapr pub/sub. Cuando el Content Creator termina, publica un evento en el tema `content-created` y el Evaluator se dispara automáticamente sin que nadie lo invoque de forma síncrona."

"Esto es importante porque enseña que en sistemas agentic reales, no todo es request/response lineal. A veces necesitas composición asíncrona, y Dapr es la forma de hacerlo de manera desacoplada."

## Entrada en la Dev UI: 3 minutos

> 🖥️ **Para este bloque:** navegador en `http://localhost:8080` (Dev UI) · terminal con `dotnet run --project Lab/src/aspire/ContentAgentFactory.AppHost` o `docker compose up` en `Lab/`

“Vamos a verlo funcionar. Entro en la Dev UI y lanzo una petición con un tema técnico. En una demo de agentes, este momento importa porque aquí se ve si realmente hay especialización o solo una fachada bonita encima de una llamada grande al modelo.”

“Cuando lanzo la petición, el primer agente no intenta contestar todavía. Lo primero que hace es buscar y reducir incertidumbre. Ese es uno de los cambios de mentalidad más útiles en aplicaciones AI-native: antes de generar, recuperamos contexto.”

Mientras enseñas la UI, puedes decir:

“Fijaos en que no quiero un sistema que improvise. Quiero un sistema que recupere señales del mundo real: Learn, blogs, actualizaciones, repositorios. Esa trazabilidad es la que luego me permite confiar más en el resultado.”

## Agente de research: 4 minutos

> 📂 **Ficheros de este bloque:** `Lab/src/agent-research/agent.py` · `Lab/src/agent-research/a2a.py` · `Lab/src/agent-research/main.py`

“Este primer agente está hecho con Python, LangGraph y FastAPI. Su trabajo consiste en buscar, ranquear y sintetizar. No genera el entregable final. Genera un brief que reduce el problema para el siguiente agente.”

“Busca en varias fuentes, usa el modelo para priorizar relevancia, descarga contenido de las páginas más prometedoras y aún da un paso más: sigue enlaces de primer nivel dentro de dominios confiables para ampliar el contexto útil.”

“Este patrón me gusta mucho para contarlo porque hace evidente que un agente no es solo un wrapper del LLM. Aquí hay estrategia de recuperación, filtros, ranking, límites de fetch, fallbacks y una salida estructurada.”

Transición recomendada:

“Con ese brief ya no le pedimos al siguiente agente que piense desde cero. Le pedimos que transforme material curado.”

## Agente creador de contenido: 4 minutos

> 📂 **Ficheros de este bloque:** `Lab/src/agent-creator/AgentCreator/ContentFactoryWorkflow.cs` · `Lab/src/agent-creator/AgentCreator/ContentCreatorAgent.cs` · `Lab/src/agent-creator/AgentCreator/Executors/BlogGenerationExecutor.cs` · `Lab/src/agent-creator/AgentCreator/Models/ContentCreatedMessage.cs`

“El segundo agente está implementado en .NET 10 con Microsoft Agent Framework. Aquí entramos en una parte que suele gustar mucho porque conecta el mundo agentic con patrones de ingeniería muy reconocibles en .NET: workflows, ejecutores, sesiones y observabilidad clara.”

“Lo que hace este agente es tomar el brief y convertirlo en un paquete de contenido: blog post, LinkedIn post y social thread. La idea no es pedir tres veces lo mismo al modelo. La idea es usar una cadena de pasos en la que primero se construye la pieza larga y después se derivan las piezas cortas.”

“Esto ilustra bien por qué un agente especializado aporta valor. No solo genera texto. Mantiene intención, estado y coherencia entre formatos distintos.”

Frase útil mientras enseñas código o estructura:

“Si esto lo metiera detrás de un solo endpoint, lo podría hacer. Pero perdería claridad sobre quién decide qué, dónde vive el estado y cómo observo la ejecución. Aquí cada responsabilidad queda bastante más explícita.”

## Agente podcaster: 4 minutos

> 📂 **Ficheros de este bloque:** `Lab/src/agent-podcaster/script_generator.py` · `Lab/src/agent-podcaster/agent.py` · `Lab/src/agent-podcaster/tts_client.py` · `Lab/src/agent-podcaster/audio_utils.py`

“El tercer agente es probablemente el más vistoso, porque nos saca del texto. Lo que recibe es el brief de investigación y lo transforma en un guion conversacional a dos voces. Después sintetiza audio.”

“Para esto se apoya en GitHub Copilot SDK en modo BYOK contra Azure OpenAI. Genera el guion, lo critica, lo refina y además ajusta pronunciaciones técnicas antes de pasar a TTS.”

“Aquí hay dos modos. En modo lab usa Azure OpenAI TTS. En modo full puede usar un servidor XTTS-v2 en GPU sobre Azure Container Apps. Esto es interesante porque enseña un patrón muy real: no todos los agentes tienen el mismo perfil de cómputo.”

“Y de nuevo, el mensaje importante no es solo que genera audio. El mensaje importante es que el output de un agente puede ser multimodal y operativo: un MP3 almacenado, no solo una cadena de texto.”
## Agente evaluator: 3 minutos

> 📂 **Ficheros de este bloque:** `Lab/src/agent-evaluator/AgentEvaluator/EvaluatorAgent.cs` · `Lab/src/agent-evaluator/AgentEvaluator/ContentEvaluationService.cs` · `Lab/src/agent-evaluator/AgentEvaluator/FoundryEvaluationService.cs` · `Lab/docs/evaluator-agent-architecture.md`

"El cuarto agente es donde cierra el bucle de calidad. Está implementado en .NET 10 con Microsoft Agent Framework, igual que el Content Creator, pero su integración es muy distinta: no recibe llamadas directas A2A, sino que escucha eventos Dapr."

"Cuando el Content Creator termina, publica un evento `content-created` en el bus de Dapr. El Evaluator lo captura y ejecuta dos flujos en paralelo: primero una evaluación rápida LLM-as-judge usando MAF, y después lanza un job a Azure AI Foundry para evals más pesadas."

"¿Por qué esto importa para la charla? Porque aquí vemos que Dapr no es solo un patrón teórico en este repo. Es la forma en que desacoplamos evaluación del pipeline principal. El Content Creator no necesita esperar ni saber si hay evaluación. El Evaluator puede cambiar, mejorar, o incluso replicarse sin afectar al resto del sistema."

"La evaluación genera scores sobre relevancia, calidad del blog post, calidad del contenido social, y feedback estructurado. Todo queda registrado en Application Insights y Azure AI Foundry, así que tienes un historial completo de decisiones y mejoras."
## Aspire, local y despliegue: 3 minutos

> 📂 **Ficheros de este bloque:** `Lab/src/aspire/ContentAgentFactory.AppHost/Program.cs` · `Lab/dapr/components/pubsub.yaml` · `infra/main.bicep` · `azure.yaml` · `Lab/docker-compose.yml`

“Hasta aquí hemos visto el comportamiento. Ahora quiero enseñar el camino de desarrollo a cloud. Este repo se puede correr localmente con Docker Compose y también con .NET Aspire AppHost para la experiencia de composición local.”

“Aspire aquí ayuda a levantar servicios, inyectar configuración y enrutar telemetría. Es una muy buena pieza para el inner loop. Luego, para cloud, el despliegue está descrito en Bicep y se ejecuta con `azd`.”

“Eso crea el runtime de Azure Container Apps, el registro, la parte de observabilidad, el storage y la capa de AI Services/Foundry que usa la solución.”

Transición recomendada:

“Pero una demo de agentes no está completa si solo enseña que funciona. Tiene que enseñar cómo se observa.”

## Observabilidad: 3 minutos

> 📂 **Ficheros de este bloque:** `Lab/docs/otel-architecture.md` · portal de Application Insights abierto en el navegador

“Todos los agentes emiten trazas y logs con OpenTelemetry. Azure Container Apps enruta esa telemetría hacia Application Insights. Esto me permite seguir la ejecución de extremo a extremo.”

“Aquí es donde normalmente una demo se vuelve creíble o deja de serlo. Si no puedo ver qué herramienta llamó un agente, qué dependencia externa tocó, cuánto tardó y cómo se encadenó la ejecución, entonces tengo una demo bonita, pero no una aplicación operable.”

“En sistemas con LLMs, observabilidad no es un nice to have. Es la diferencia entre poder iterar y estar adivinando.”

## Foundry: 3 minutos

> 📂 **Ficheros de este bloque:** `Lab/sample-output/evals.jsonl` · portal de Azure AI Foundry abierto en el navegador

“Después de observar, toca gobernar. Microsoft Foundry entra aquí como plano de modelos, registro y evaluación. No sustituye al runtime de contenedores. Lo complementa.”

“En esta demo, Foundry nos sirve para consumir modelos, registrar activos y ejecutar evaluaciones sobre datasets. Eso cierra muy bien la narrativa de pasar de prototipo local a un sistema que ya tiene una historia operativa más seria.”

“Esto me interesa mucho remarcarlo: en una app AI-native, escoger el modelo es solo una parte del problema. La otra parte es registrar, observar, evaluar y gobernar.”

## Cierre técnico: 2 minutos

“Si os quedáis con una sola idea, que sea esta: no estamos dejando de usar APIs. Estamos dejando de usar APIs como la única unidad de diseño. Para flujos AI-native complejos, la unidad útil pasa a ser el agente.”

“Un agente aquí significa capacidad especializada, herramientas concretas, contexto delimitado, telemetría, despliegue independiente y posibilidad de evaluación. Y cuando montas varias de esas piezas sobre Azure Container Apps, con Aspire para el desarrollo local y Foundry para operación y evaluación, empiezas a tener una arquitectura bastante más realista para este tipo de aplicaciones.”

## Cierre corto por si vas justo de tiempo

“La idea no es reemplazar HTTP. La idea es dejar de modelar toda la aplicación como si un endpoint fuese suficiente. Este proyecto enseña que una app AI-native gana mucho cuando separas investigación, generación y salida multimodal en agentes observables y desplegables por separado.”

## Mensajes que más conviene remarcar

1. Multiagente no significa magia; significa separar responsabilidades.
2. La calidad sube cuando el primer paso es recuperar contexto y fuentes.
3. Azure Container Apps simplifica ejecutar agentes heterogéneos.
4. Aspire mejora el inner loop local.
5. Observabilidad no es opcional en sistemas con LLMs.
6. Foundry aporta operación, trazas y evaluación, no solo acceso a modelos.

## Qué no conviene prometer

1. Que el evaluator sea sincrónico; es asincrónico y puede haber latencia entre publicación y evaluación.
2. Que todo sea completamente autónomo sin supervisión, porque hay prompts, límites, fallbacks y diseño explícito.
3. Que sustituir APIs signifique eliminar HTTP; significa subir el nivel de abstracción.
4. Que Dapr es "complicado"; aquí ves que es simplemente pub/sub, un patrón muy natural para desacoplar agentes.

## Demo flow sugerido para 30-35 minutos

1. Apertura con problema y tesis.
2. Arquitectura general: 4 agentes, A2A + Dapr pub/sub.
3. Dev UI lanzando la petición.
4. Research Agent: grounding, ranking y brief.
5. Content Creator: workflow y salidas multi-formato.
6. Podcaster: guion, TTS y artefacto multimodal.
7. Evaluator: eventos Dapr, LLM-as-judge y Foundry evals.
8. Application Insights: trazas correladas de los 4 agentes.
9. Azure AI Foundry: evaluaciones, datasets y portal.
10. Aspire local y despliegue con `azd` + Bicep.
11. Cierre con tradeoffs reales y siguientes pasos.

## Preguntas que nos harán

1. ¿Por qué usar agentes y no una API tradicional con varios endpoints?
2. ¿Qué gana esta arquitectura frente a un único prompt muy grande?
3. ¿Cómo evitáis alucinaciones o contenido inventado?
4. ¿Qué parte del sistema depende realmente de Azure AI Foundry?
5. ¿Qué papel juega Azure Container Apps frente a AKS o App Service?
6. ¿Dónde encaja .NET Aspire en el ciclo de desarrollo?
7. ¿Por qué la comunicación entre agentes es A2A y no REST puro?
8. ¿Cómo funciona el integrador Dapr pub/sub en esta arquitectura?
9. ¿Cómo versionáis prompts, instrucciones y contratos entre agentes?
10. ¿Cómo observáis qué hizo cada agente y cuánto costó en tokens?
11. ¿Qué ocurre si falla uno de los agentes a mitad del pipeline?
12. ¿Cómo autenticáis y protegéis los endpoints A2A?
13. ¿Qué partes del flujo son síncronas y cuáles asíncronas?
14. ¿Cómo y cuándo se dispara el agente evaluator?
15. ¿Cuál es la diferencia entre las evaluaciones LLM-as-judge y las de Azure AI Foundry?
16. ¿Qué limita hoy llevar este ejemplo a producción?
17. ¿Cómo controláis coste cuando hay varios agentes, búsquedas, evaluaciones y TTS?
18. ¿Podríais cambiar Azure OpenAI por otro proveedor de modelos?
19. ¿Qué ventajas tiene separar agentes por lenguaje y stack?
20. ¿Qué latencia total tiene el flujo completo y dónde está el cuello de botella?
21. ¿Cuál sería el siguiente paso para evolucionar esta demo a una plataforma agentic real?

## Respuestas cortas sugeridas

1. Agentes permiten separar responsabilidades, contexto y herramientas; una API clásica suele mezclarlo todo en una única capa.
2. Un único prompt escala peor en control, trazabilidad y mantenibilidad.
3. Grounding con fuentes reales, ranking, fetch de contenido, fallbacks y evaluación posterior.
4. Foundry aporta modelos, registro, evaluación y plano operativo; no sustituye al runtime de contenedores.
5. Container Apps reduce fricción operativa para contenedores y encaja bien con workloads event-driven o HTTP.
6. Aspire acelera el inner loop local y la composición de servicios.
7. A2A está más cerca del contrato de capacidades entre agentes que de un API de recursos.
8. El Content Creator publica eventos a Dapr pub/sub (`content-created`), el Evaluator se suscribe. Es desacoplamiento asincrónico puro.
9. Ahora están en código y config; en producción conviene versionarlos como artefactos explícitos.
10. Con OTEL, Application Insights y trazas correladas por servicio.
11. Hay retries, fallbacks y límites por etapa, pero no es una orquestación transaccional clásica.
12. Con bearer token o API key opcional según configuración A2A.
13. Research, Creator y Podcaster son síncronos (request/response). Evaluator es asincrónico (event-driven vía Dapr).
14. El Evaluator se dispara automáticamente cuando recibe el evento `content-created` publicado por el Content Creator.
15. LLM-as-judge es local, rápido y usa MAF. Foundry evals es cloud, puede ser más compleja, y el resultado queda en Foundry para histórico.
16. Hardening de seguridad, SLOs, control de costes, versionado y gobernanza más estricta.
17. Separando modos lab/full, acotando fetch, usando top-k, controlando TTS/GPU y optimizando numero de evals.
18. En parte sí, siempre que adaptes clientes, prompts y observabilidad de proveedor.
19. Cada agente usa el stack que mejor resuelve su problema.
20. Normalmente el cuello está en fetch externo, generación larga y TTS; evals suma overhead pero es asincrónico.
21. Añadir memoria, policy, versionado de agentes, evaluaciones continuas, automatización de mejoras basadas en evals.

## Cierre sugerido

La tesis de la charla puede resumirse así: no estamos dejando de usar APIs, estamos dejando de diseñar el sistema alrededor de APIs como única unidad de valor. En aplicaciones AI-native, la unidad interesante pasa a ser el agente: con objetivos, herramientas, contexto, observabilidad y despliegue independiente.