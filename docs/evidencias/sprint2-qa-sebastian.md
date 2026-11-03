# Sprint 2 — QA e infraestructura (Sebastián Miranda)

Primera entrega: rama `feature/sprint2-sebastian` (PR #8, sobre `c7b5c76`, S2-S01 a S2-S04, S2-S09 parcial y S2-S10).
Segunda entrega: rama `feature/sprint2-sebastian-2`, basada en `develop` `f77ef0a` (ya con el código de Jair y Paul): S2-S05 a S2-S08 y S2-S09 completo.
Las pruebas se ejecutaron el 3 de noviembre de 2026 con .NET 8.0 y Docker (SQL Server 2022 y RabbitMQ 3.13 reales vía Testcontainers).

## Resultado de las pruebas

| Suite | Tipo | Resultado |
|---|---|---|
| Siha.MsA.Tests | Unitarias (simuladas) | 113 / 113 |
| Siha.MsB.Tests | Unitarias (simuladas) | 67 / 67 |
| Siha.MsC.Tests | Unitarias (simuladas) | **195 / 195** (143 en `develop` + 52 nuevas) |
| Siha.Integracion.Tests | Infraestructura real | **38 / 38** (25 anteriores + 13 nuevas) |

Compilación de `SIHA-IA.sln`: 0 advertencias, 0 errores.

## Tareas

| Tarea | Historia | Estado | Qué se entregó |
|---|---|---|---|
| S2-S01 | US-07 | Hecha | Servicio `ollama` en `docker-compose.yml` (perfil `sprint23`): descarga el modelo de lenguaje y el de embeddings al primer arranque, volumen `siha-ollama` y verificación de salud que solo pasa cuando los modelos están listos. `ms-c` recibe `Ollama__*` y `ReglasGrpc__Url`. Variables opcionales en `.env.example`. Configuración `OllamaOptions` en el Microservicio C. Las migraciones de DB C (PROCESSED_EVENTS, EXTRACTION, EXTRACTION_UNIT) se comprueban en SQL Server real. |
| S2-S02 | US-07 | Hecha | Idempotencia con SQL Server real: 10 entregas simultáneas del mismo expediente dejan **una** fila en PROCESSED_EVENTS (la clave primaria resuelve la carrera; EF InMemory no puede demostrarlo). PDF con hash alterado no se registra. Expedientes distintos simultáneos se registran todos. |
| S2-S03 | US-08 | Hecha | 10 pruebas unitarias del cliente gRPC de C (éxito, recuperación tras caída, timeout persistente, payload inválido, contrato incompatible, ruta sin parámetros, cancelación, deadline, error de transporte, latencia) y 4 de integración contra el Microservicio A real (cambio de umbral visible con nueva versión, ruta inválida rechazada, A caído → error controlado, latencia p95). |
| S2-S04 | US-09 | Hecha | 20 casos borde de `ReglaCreditosStrategy`: redondeo, créditos cero, desbordamiento, denominador negativo, créditos externos sin conversión de 48 h, unión N:1, cuarta matrícula, combinaciones de controles y determinismo. |
| S2-S05 | US-10 | Hecha | Caché de embeddings en DB C: tabla **EMBEDDING_UNIDAD** (migración `CacheEmbeddingsUnidad`, índice único asignatura + unidad + modelo) y `EmbeddingCacheService`. El vector se guarda una vez por unidad; solo se reutiliza si el modelo y el SHA-256 del texto coinciden, así un cambio en el plan lo recalcula. Si Ollama no responde no se guarda ni se inventa nada. `POST /api/extracciones/embeddings/precalcular` (solo administrador) precarga los planes del catálogo. El analizador toma de la caché las unidades destino que llegan sin vector. 14 pruebas unitarias (incluye la precarga de 43 planes × 4 unidades: 172 llamadas la primera vez y 0 la segunda) y 1 con SQL Server real (10 escrituras simultáneas → 1 fila; 768 dimensiones = 3072 bytes). |
| S2-S06 | US-10 | Hecha | 20 pruebas deterministas de `ReglaContenidoStrategy` con vectores construidos a mano (coseno exacto): similitud **igual** a τ cubre y por debajo no (75 frente a 75,0001); 6/7 con horas distintas = 92,31 % (no 85,71 %); plan sin horas → Pcont 0 sin división por cero; vector en ceros y vectores opuestos sin NaN ni similitud negativa; invariancia al orden y a la escala; 100 ejecuciones idénticas; redondeo a 2 decimales; τ de gRPC informado tal cual. No repiten los casos de `ReglaContenidoStrategyTests`. |
| S2-S07 | US-07 / US-10 | Hecha | 18 pruebas de estrés y tiempo límite contra un Ollama simulado por HTTP: un modelo que tarda 30 s corta en el timeout configurado (≈1 s) y devuelve null; la cancelación del usuario se propaga; errores 500/503, JSON vacío o corrupto → null; 200 peticiones de embeddings simultáneas terminan todas; con fallos 1 de cada 3 no se mezclan vectores; OCR + LLM lento cae al parser de respaldo y pide confirmación humana; planes de 1, 10 y 40 páginas llegan completos en una sola llamada; 50 extracciones de ~200 KB no retienen memoria (< 20 MB); 20 extracciones simultáneas con modelo lento terminan en < 8 s. |
| S2-S08 | US-11 | Hecha | 10 pruebas del repositorio de precedentes con SQL Server real: las 4 migraciones de DB C se aplican sobre una base vacía; coincidencia exacta sin importar orden ni mayúsculas; tabla vacía → []; falta o sobra una asignatura, otro destino, otra institución o consulta vacía → [] (no se inventan referencias); fila con JSON corrupto se ignora; **base caída → error explícito** (SqlException en ~2 s), nunca una lista vacía que parezca "sin precedentes". |
| S2-S09 | US-11 | Hecha | Además del flujo **B → RabbitMQ → C** del primer PR, recorrido completo con infraestructura real: evento → recepción idempotente en C → extracción del plan del volumen → el análisis se niega sin confirmación (ExtraccionNoConfirmada) → confirmación del operador por la API → θ/δ/τ por gRPC desde el Microservicio A real → Pcred 100 % → Pcont 75 % (3 de 4 unidades, destino desde la caché) → precedente RES-FISEI-2025-031 → **ListaParaDictamen**. Un cambio de τ en A (75 → 99) se aplica en la siguiente evaluación sin reiniciar C. Con A caído el análisis termina en **ErrorReglasGrpc**, con Pcont 0 y sin precedentes. |
| S2-S10 | US-11 | Hecha | El job de unitarias del CI ejecuta `Siha.MsC.Tests`; el job de integración ya incluye las pruebas nuevas. |

## Cambios de soporte en el Microservicio C

- `RabbitMq:ColaSolicitudCreada` permite cambiar el nombre de la cola. En Docker sigue siendo `solicitud-creada-ms-c`; las pruebas usan una cola por instancia para no mezclar mensajes.
- Se registra `OllamaOptions` (sección `Ollama`).
- `AgenteAnalizadorService` recibe la caché como parámetro opcional al final del constructor: las pruebas de Jair y Paul que lo construyen a mano siguen compilando sin cambios, y una unidad destino que ya trae su vector se usa tal cual.
- La migración `Precedentes` (S2-J08) no actualizó `SihaDbContextCModelSnapshot.cs`; por eso EF intentaba volver a crear PRECEDENTE en la siguiente migración. `CacheEmbeddingsUnidad` deja el snapshot al día y solo crea EMBEDDING_UNIDAD. La prueba de las 4 migraciones sobre base vacía lo vigila.

## Observaciones

- La descarga de modelos de Ollama no se pudo probar en el entorno de CI de esta verificación porque el contenedor no tiene salida a `registry.ollama.ai`. El contenedor arranca, el servidor escucha en 11434 y la verificación de salud queda en `starting` mientras falten los modelos, que es el comportamiento esperado. En un equipo con Internet: `docker compose --profile sprint23 up -d ollama` y luego `docker exec siha-ollama ollama list`.
- `llama3.2:3b` necesita unos 4 GB de RAM libres. En equipos con menos memoria, poner `SIHA_OLLAMA_MODELO=qwen2.5:1.5b` en `.env`.
- Los mensajes `Failed executing DbCommand` en la salida de la prueba de entregas simultáneas son los intentos duplicados que SQL Server rechaza por la clave primaria: es el comportamiento que la prueba verifica.

## Hallazgos para el equipo

No se cambió el código de Jair ni de Paul; quedan anotados para decidir en el equipo.

1. El consumidor de `SolicitudCreadaEvent` registra el evento en PROCESSED_EVENTS pero todavía no llama al extractor ni crea la fila EXTRACTION; ningún código de producción la crea. La prueba completa hace ese paso con el `ExtractorPdfService` del propio C. Falta conectarlo en `ProcesarSolicitudCreadaService`.
2. Si Ollama no da el vector de ninguna unidad de origen, `ReglaContenidoStrategy` lanza `ArgumentException` y la API responde 400 con el motivo. Es un fallo controlado, pero convendría un estado propio (por ejemplo "pendiente de embeddings") en vez de 400.
3. `OcrLlmExtractorService` atrapa todas las excepciones, también la cancelación del usuario, y sigue con el parser de respaldo. No es un error de datos, pero la petición no se corta al cancelarla.
4. El plan de prueba con todos los campos sale con confianza 1,00, que supera el umbral 0,80 y el extractor lo sugeriría como CONFIRMADA sin revisión. La prueba completa lo guarda como pendiente para pasar por la confirmación del operador; el equipo debe decidir si ese auto-aprobado es lo que se quiere (CA de US-07).
5. Un plan destino sin horas da Pcont 0 sin avisar. El catálogo de A ya marca esos planes como incompletos (`PlanCompleto = false`); sería bueno que el analizador lo informe como alerta.

## Cómo reproducir

```bash
dotnet test tests/Siha.MsC.Tests -c Release
dotnet test tests/Siha.Integracion.Tests -c Release --filter "FullyQualifiedName~MicroservicioC|FullyQualifiedName~ReglasGrpcMsC|FullyQualifiedName~PrecedentesYCacheReal|FullyQualifiedName~FlujoCompletoAnalisis"
```

Precarga de la caché en Docker (token de administrador):

```bash
curl -X POST http://localhost:5000/api/extracciones/embeddings/precalcular \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '[{"codigoAsignatura":"SW201","unidades":[{"numero":1,"contenidos":"Listas y pilas"}]}]'
```
