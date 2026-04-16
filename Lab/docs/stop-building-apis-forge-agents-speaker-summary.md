# Speaker Summary

## Mensaje principal

Este proyecto demuestra una aplicación AI-native donde el trabajo se reparte entre agentes especializados en vez de concentrarse en un backend tradicional orientado a endpoints.

## Qué enseñar

1. Dev UI lanzando un tema.
2. Research Agent recopilando y priorizando fuentes reales.
3. Content Creator generando blog y social content.
4. Podcaster creando guion y audio.
5. Telemetría en Application Insights.
6. Registro y evaluación en Microsoft Foundry.

## Idea fuerza

La abstracción principal ya no es el endpoint, sino el agente con capacidades, estado, herramientas y trazabilidad.

## Encaje con la charla

1. Azure Container Apps es el runtime de ejecución.
2. .NET Aspire sirve para componer y ejecutar localmente.
3. OpenTelemetry y Azure Monitor muestran qué hacen los agentes.
4. Microsoft Foundry aporta modelos, registro y evaluación.

## Matiz importante

El repo sí usa Aspire y sí encaja muy bien con Foundry y observabilidad. La comunicación visible en el código entre agentes está basada sobre todo en A2A por HTTP/JSON-RPC. Dapr no aparece aquí como pieza central, así que conviene presentarlo como patrón complementario o siguiente evolución, no como base del flujo actual.

## Frase útil de cierre

No dejamos de usar HTTP; dejamos de diseñar la solución como si HTTP y CRUD fueran la unidad principal del sistema. Aquí la unidad principal es el agente.