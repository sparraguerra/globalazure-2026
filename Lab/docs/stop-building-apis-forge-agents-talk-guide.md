# Stop Building APIs. Forge Agents with Azure

## Qué es este proyecto

Este repositorio implementa una factoría de contenido multiagente desplegable en Azure. A partir de un tema técnico, la solución investiga fuentes reales, genera contenido editorial y, opcionalmente, crea un podcast con audio sintetizado.

La idea central no es exponer un único endpoint que haga todo, sino orquestar varias capacidades autónomas especializadas que colaboran entre sí:

1. Un agente investiga y construye un brief fiable.
2. Otro agente transforma ese brief en piezas de contenido.
3. Un tercer agente convierte ese resultado en un formato conversacional y audio.

El proyecto sirve como demo de una aplicación AI-native que combina agentes, modelos, telemetría y despliegue cloud sin esconder los detalles de implementación.

## Qué hace exactamente

El flujo funcional es este:

1. El usuario escribe un tema en la Dev UI.
2. El agente de research busca en Microsoft Learn, Azure Blog, Tech Community, Azure Updates y GitHub.
3. Ese agente prioriza resultados con un LLM, descarga contenido relevante y sintetiza un brief estructurado.
4. El agente creador convierte ese brief en blog post, post de LinkedIn y social thread.
5. El agente podcaster, si está habilitado, reusa el brief para producir un guion conversacional y generar audio.
6. La solución muestra resultados, conserva trazas y permite evaluar salidas en Microsoft Foundry.

El valor de demo está en que cada etapa produce un artefacto distinto y verificable: fuentes, brief, contenido, guion, audio y telemetría.

## Cómo funciona por dentro

## Arquitectura general

La arquitectura lógica es lineal, pero la implementación está desacoplada:

`Dev UI -> Research Agent -> Content Creator Agent -> Podcaster Agent`

Cada agente se ejecuta en su propio contenedor y expone dos superficies:

1. Un agent card en `/.well-known/agent.json` para descubrimiento.
2. Un endpoint `/a2a` basado en JSON-RPC para recibir tareas.

Eso hace que la unidad de integración no sea un contrato REST tradicional centrado en recursos, sino un agente con capacidades declaradas y una interfaz orientada a tareas.

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

Aquí conviene ser preciso.

Aspire sí aparece en el repo como experiencia de orquestación local mediante el AppHost de .NET Aspire. Te sirve para mostrar composición local, variables de entorno compartidas y telemetría.

Dapr, en cambio, no aparece como el mecanismo principal de comunicación en el código actual. La comunicación entre agentes que se ve en el repo está centrada en A2A sobre HTTP/JSON-RPC y agent cards, no en sidecars Dapr ni componentes Dapr visibles en la infraestructura del proyecto.

Por tanto, para no generar fricción en la charla, tienes dos opciones válidas:

1. Presentar Aspire como parte demostrable del repo y Dapr como patrón complementario o evolución natural para pub/sub, state store o bindings.
2. Ajustar el discurso y decir que en esta demo la colaboración agente-a-agente se resuelve con A2A, mientras que Dapr es una pieza opcional para escenarios donde quieras más building blocks distribuidos.

La segunda opción es la más honesta respecto al estado actual del código.

## Guion recomendado para contarlo en directo

## Apertura

“Esta demo enseña cómo pasar de una aplicación compuesta por endpoints a una aplicación compuesta por agentes especializados. En vez de pedirle a un backend que haga todo, distribuimos el trabajo entre agentes con contratos, herramientas, telemetría y despliegue independiente.”

## Recorrido técnico

1. Enseña la Dev UI y dispara un tema.
2. Explica que el primer agente no responde, investiga.
3. Muestra que el segundo agente no improvisa: transforma un brief en contenido.
4. Usa el tercer agente para enseñar un resultado multimodal.
5. Salta a Application Insights para ver trazas.
6. Cierra en Foundry con registro/evaluación para enseñar gobernanza.

## Mensajes que más conviene remarcar

1. Multiagente no significa magia; significa separar responsabilidades.
2. La calidad sube cuando el primer paso es recuperar contexto y fuentes.
3. Azure Container Apps simplifica ejecutar agentes heterogéneos.
4. Aspire mejora el inner loop local.
5. Observabilidad no es opcional en sistemas con LLMs.
6. Foundry aporta operación, trazas y evaluación, no solo acceso a modelos.

## Qué no conviene prometer

1. Que Dapr sea hoy la base operativa del repo, porque no lo es.
2. Que todo sea completamente autónomo sin supervisión, porque hay prompts, límites, fallbacks y diseño explícito.
3. Que sustituir APIs signifique eliminar HTTP; significa subir el nivel de abstracción.

## Demo flow sugerido para 25-30 minutos

1. Problema: por qué un endpoint único se queda corto para flujos AI-native.
2. Arquitectura: tres agentes, tres runtimes, un pipeline.
3. Código: vistazo rápido a Research, Creator y Podcaster.
4. Orquestación local: Aspire o Docker Compose.
5. Despliegue: `azd up` + Bicep.
6. Ejecución en vivo desde la Dev UI.
7. Observabilidad con Application Insights / OTEL.
8. Registro y evaluación en Foundry.
9. Cierre con tradeoffs reales.

## Preguntas que nos harán

1. ¿Por qué usar agentes y no una API tradicional con varios endpoints?
2. ¿Qué gana esta arquitectura frente a un único prompt muy grande?
3. ¿Cómo evitáis alucinaciones o contenido inventado?
4. ¿Qué parte del sistema depende realmente de Azure AI Foundry?
5. ¿Qué papel juega Azure Container Apps frente a AKS o App Service?
6. ¿Dónde encaja .NET Aspire en el ciclo de desarrollo?
7. ¿Por qué la comunicación entre agentes es A2A y no REST puro?
8. ¿Dónde entra Dapr en esta historia y por qué no se ve tanto en la demo?
9. ¿Cómo versionáis prompts, instrucciones y contratos entre agentes?
10. ¿Cómo observáis qué hizo cada agente y cuánto costó en tokens?
11. ¿Qué ocurre si falla uno de los agentes a mitad del pipeline?
12. ¿Cómo autenticáis y protegéis los endpoints A2A?
13. ¿Qué partes del flujo son síncronas y cuáles asíncronas?
14. ¿Cómo evaluáis la calidad del contenido generado?
15. ¿Qué limita hoy llevar este ejemplo a producción?
16. ¿Cómo controláis coste cuando hay varios agentes, búsquedas y TTS?
17. ¿Podríais cambiar Azure OpenAI por otro proveedor de modelos?
18. ¿Qué ventajas tiene separar agentes por lenguaje y stack?
19. ¿Qué latencia total tiene el flujo completo y dónde está el cuello de botella?
20. ¿Cuál sería el siguiente paso para evolucionar esta demo a una plataforma agentic real?

## Respuestas cortas sugeridas

1. Agentes permiten separar responsabilidades, contexto y herramientas; una API clásica suele mezclarlo todo en una única capa.
2. Un único prompt escala peor en control, trazabilidad y mantenibilidad.
3. Grounding con fuentes reales, ranking, fetch de contenido, fallbacks y evaluación posterior.
4. Foundry aporta modelos, registro, evaluación y plano operativo; no sustituye al runtime de contenedores.
5. Container Apps reduce fricción operativa para contenedores y encaja bien con workloads event-driven o HTTP.
6. Aspire acelera el inner loop local y la composición de servicios.
7. A2A está más cerca del contrato de capacidades entre agentes que de un API de recursos.
8. En este repo Dapr es más una extensión posible que el núcleo actual de la comunicación.
9. Ahora están en código y config; en producción conviene versionarlos como artefactos explícitos.
10. Con OTEL, Application Insights y trazas correladas por servicio.
11. Hay retries, fallbacks y límites por etapa, pero no es una orquestación transaccional clásica.
12. Con bearer token o API key opcional según configuración A2A.
13. Hay mezcla: research y creator son request/response; podcaster introduce ejecución asíncrona para tareas largas.
14. Con datasets JSONL y evaluaciones en Foundry, además de revisión humana.
15. Hardening de seguridad, SLOs, control de costes, versionado y gobernanza más estricta.
16. Separando modos lab/full, acotando fetch, usando top-k y controlando TTS/GPU.
17. En parte sí, siempre que adaptes clientes, prompts y observabilidad de proveedor.
18. Cada agente usa el stack que mejor resuelve su problema.
19. Normalmente el cuello está en fetch externo, generación larga y TTS.
20. Añadir memoria, policy, versionado de agentes, evaluaciones continuas y mejores patrones de operación.

## Cierre sugerido

La tesis de la charla puede resumirse así: no estamos dejando de usar APIs, estamos dejando de diseñar el sistema alrededor de APIs como única unidad de valor. En aplicaciones AI-native, la unidad interesante pasa a ser el agente: con objetivos, herramientas, contexto, observabilidad y despliegue independiente.