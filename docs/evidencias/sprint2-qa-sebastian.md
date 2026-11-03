# Sprint 2 — QA e infraestructura (Sebastián Miranda)

Rama `feature/sprint2-sebastian`, basada en `develop` (commit `c7b5c76`, con el Microservicio C de S2-J01 a S2-J04).
Las pruebas se ejecutaron el 2 de noviembre de 2026 con .NET 8.0 y Docker (SQL Server 2022 y RabbitMQ 3.13 reales vía Testcontainers).

## Resultado de las pruebas

| Suite | Tipo | Resultado |
|---|---|---|
| Siha.MsA.Tests | Unitarias (simuladas) | 113 / 113 |
| Siha.MsB.Tests | Unitarias (simuladas) | 67 / 67 |
| Siha.MsC.Tests | Unitarias (simuladas) | **74 / 74** (44 existentes + 30 nuevas) |
| Siha.Integracion.Tests | Infraestructura real | **25 / 25** (16 del Sprint 1 + 9 nuevas) |

Compilación de `SIHA-IA.sln`: 0 advertencias, 0 errores.

## Tareas

| Tarea | Historia | Estado | Qué se entregó |
|---|---|---|---|
| S2-S01 | US-07 | Hecha | Servicio `ollama` en `docker-compose.yml` (perfil `sprint23`): descarga el modelo de lenguaje y el de embeddings al primer arranque, volumen `siha-ollama` y verificación de salud que solo pasa cuando los modelos están listos. `ms-c` recibe `Ollama__*` y `ReglasGrpc__Url`. Variables opcionales en `.env.example`. Configuración `OllamaOptions` en el Microservicio C. Las migraciones de DB C (PROCESSED_EVENTS, EXTRACTION, EXTRACTION_UNIT) se comprueban en SQL Server real. |
| S2-S02 | US-07 | Hecha | Idempotencia con SQL Server real: 10 entregas simultáneas del mismo expediente dejan **una** fila en PROCESSED_EVENTS (la clave primaria resuelve la carrera; EF InMemory no puede demostrarlo). PDF con hash alterado no se registra. Expedientes distintos simultáneos se registran todos. |
| S2-S03 | US-08 | Hecha | 10 pruebas unitarias del cliente gRPC de C (éxito, recuperación tras caída, timeout persistente, payload inválido, contrato incompatible, ruta sin parámetros, cancelación, deadline, error de transporte, latencia) y 4 de integración contra el Microservicio A real (cambio de umbral visible con nueva versión, ruta inválida rechazada, A caído → error controlado, latencia p95). |
| S2-S04 | US-09 | Hecha | 20 casos borde de `ReglaCreditosStrategy`: redondeo, créditos cero, desbordamiento, denominador negativo, créditos externos sin conversión de 48 h, unión N:1, cuarta matrícula, combinaciones de controles y determinismo. |
| S2-S09 | US-11 | Hecha (parte disponible) | Flujo asíncrono real **B → RabbitMQ → C**: el expediente registrado y enviado en B llega a C una sola vez, con el mismo CorrelationId y MessageId = ExpedienteId. Los pasos de reglas, contenidos y precedentes se agregarán cuando existan en `develop`. |
| S2-S10 | US-11 | Hecha | El job de unitarias del CI ejecuta `Siha.MsC.Tests`; el job de integración ya incluye las pruebas nuevas. |
| S2-S05 | US-10 | Pendiente | Depende de S2-J05 (Semantic Kernel + embeddings). |
| S2-S06 | US-10 | Pendiente | Depende de `ReglaContenidoStrategy` (S2-J06). |
| S2-S07 | US-10 | Pendiente | Depende del pipeline OCR + LLM (S2-P05). |
| S2-S08 | US-11 | Pendiente | Depende del repositorio de precedentes (S2-J08). |

## Cambios de soporte en el Microservicio C

- `RabbitMq:ColaSolicitudCreada` permite cambiar el nombre de la cola. En Docker sigue siendo `solicitud-creada-ms-c`; las pruebas usan una cola por instancia para no mezclar mensajes.
- Se registra `OllamaOptions` (sección `Ollama`). Todavía no se llama a Ollama desde el código.

## Observaciones

- La descarga de modelos de Ollama no se pudo probar en el entorno de CI de esta verificación porque el contenedor no tiene salida a `registry.ollama.ai`. El contenedor arranca, el servidor escucha en 11434 y la verificación de salud queda en `starting` mientras falten los modelos, que es el comportamiento esperado. En un equipo con Internet: `docker compose --profile sprint23 up -d ollama` y luego `docker exec siha-ollama ollama list`.
- `llama3.2:3b` necesita unos 4 GB de RAM libres. En equipos con menos memoria, poner `SIHA_OLLAMA_MODELO=qwen2.5:1.5b` en `.env`.
- Los mensajes `Failed executing DbCommand` en la salida de la prueba de entregas simultáneas son los intentos duplicados que SQL Server rechaza por la clave primaria: es el comportamiento que la prueba verifica.

## Cómo reproducir

```bash
dotnet test tests/Siha.MsC.Tests -c Release
dotnet test tests/Siha.Integracion.Tests -c Release --filter "FullyQualifiedName~MicroservicioC|FullyQualifiedName~ReglasGrpcMsC"
```
