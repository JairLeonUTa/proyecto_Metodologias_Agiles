# Evidencias de ejecución · Documentos, infraestructura y QA técnico

**Fecha de ejecución:** 19 de octubre de 2026 · **Responsable:** Rolando Sebastián Miranda Baño (Backend Engineer & QA/DevOps Lead)
**Entorno:** Linux x86_64 · Docker 29.9.1 · Docker Compose 5.6.0 · .NET SDK 8.0 · Node 20
**Cómo reproducir:** `docs/guia-ejecucion-documentos-e-infraestructura.md`

## Resumen

| Tipo | Suite | Infraestructura usada | Resultado | Archivo |
|---|---|---|---|---|
| **Simulada** | `Siha.MsA.Tests` | EF Core InMemory, mocks | **53/53** | `simuladas-msa-20261019.trx` |
| **Simulada** | `Siha.MsB.Tests` | EF Core InMemory, Moq, arnés en memoria de MassTransit, inspección de configuración | **60/60** | `simuladas-msb-20261019.trx` |
| **Real** | `Siha.Integracion.Tests` | SQL Server 2022 + RabbitMQ 3.13 en contenedores (Testcontainers), disco real, MsA y MsB completos | **15/15** (8 documentos/bus + 7 gRPC de reglas) | `real-integracion-20261019.trx` y `.log` |
| **Real (E2E)** | `scripts/e2e-docker-compose.sh` | Pila completa de `docker-compose.yml` desde cero (`down -v` + `up`) | **31/31** | `e2e-compose-20261019-235034.md` y `.log` |
| **Real (API)** | Colección Postman (Newman) contra la pila de Compose | Gateway + MsA + MsB + SQL + RabbitMQ | 156/159 aserciones; **carpetas 04 y 05 (expedientes y bus) sin fallos** | `real-postman-newman-20261019.log` |

Las 3 aserciones fallidas de Newman pertenecen a la carpeta **03 · Catálogo** (fuera de este alcance) y se
informan a su responsable:

1. «Crear carrera QA · Código en mayúsculas»: el test compara contra el texto literal `{{PREFIJO}}`
   (la variable no se reemplaza dentro del script de prueba).
2. y 3. «PT-15 · Hallazgo H-01»: el test espera **404** al importar por el Gateway, pero la ruta ya
   responde **200**. El hallazgo H-01 parece corregido; hay que actualizar el caso.

## Tareas del Sprint 1 asignadas (planificación SIHA-IA) y su evidencia

| Tarea | Descripción (planificación) | Evidencia |
|---|---|---|
| **S1-S01** | Configuración de contenedores Docker Compose para bases de datos y servicios | `docker-compose.yml` / `.override.yml`; E2E sección 1 (arranque con volúmenes vacíos, 7 contenedores sanos) |
| **S1-S02** | Autenticación JWT con claims de rol y expiración | `Siha.MsA.Tests/SeguridadTests`, `RbacTests` (simuladas); E2E «Login vía Gateway (JWT emitido por MsA)»; Newman carpetas 00–01 |
| **S1-S03** | Pruebas unitarias RBAC en xUnit y esquema DB B | `simuladas-msa-*.trx`, `simuladas-msb-*.trx`; esquema DB B: E2E «DB B migrada (3 migraciones)» e «Índice único filtrado» |
| **S1-S04** | Servidor gRPC de reglas en Microservicio A | `ReglasGrpcRealTests` (7 pruebas reales: umbrales y reglas = REST, filtro por trámite, `InvalidArgument`, cambio por REST visible en gRPC y persistido en SQL Server); E2E sección 3b (llamada gRPC real con `grpcurl` a `ms-a:8081`) |
| **S1-S05** | Alta de solicitud interna/externa y validación de PDF en B | E2E sección 4 (borrador 201, rechazo de documentos declarados, carga rechazada sin `%PDF`, envío 422/202); `Recorrido_completo_…` |
| **S1-S06** | Almacenamiento seguro de PDF y metadatos en DB B | E2E «SHA-256 del servidor», «Archivo en el volumen con el mismo hash», «PDF intacto tras recrear ms-b»; `Un_fallo_del_almacenamiento_responde_503_…`, `No_se_publica_si_el_PDF_ya_no_existe_…` |
| **S1-S07** | Pruebas de integración del API Gateway y conectividad del bus RabbitMQ | E2E: todas las llamadas pasan por Ocelot (`localhost:5000`) y por Nginx → Gateway; «Intercambio del contrato declarado en RabbitMQ»; `Recorrido_completo_…` con suscriptor independiente |
| **S1-S08** | Pipeline de CI y pruebas automatizadas | `.github/workflows/ci.yml`: jobs unitarias, frontend, integración real y E2E Compose, con artefactos de resultados |
| **S1-S09** | Pruebas integradas de reglas, expedientes, PDF y evento | `real-integracion-20261019.*` (15/15): reglas por gRPC + expedientes + PDF + evento en RabbitMQ real |
| **S1-S10** | Arranque limpio en Docker Compose, consolidación de QA y guía de ejecución | E2E desde cero (31/31), este documento y la guía de ejecución |

## Qué demuestra cada evidencia real frente a lo pedido por el líder

| Requisito | Evidencia |
|---|---|
| 1. Flujo registrar → almacenar/validar PDF → publicar con requisitos | E2E «Registro del borrador», «Envío sin documentos bloqueado (422)», «Carga de PDF real», «Envío con documento verificado (202)»; `Recorrido_completo_…` |
| 2. Referencias al bus = documentos existentes y aceptados; sin confiar en el cliente | E2E «Rechazo de documentos declarados por el cliente (400)», «SHA-256 calculado por el servidor»; `No_se_publica_si_el_PDF_ya_no_existe_…` y aserciones sobre `Documentos` del mensaje real |
| 3. Cargas rechazadas, fallos de almacenamiento y reintentos sin duplicar | E2E «Carga rechazada (firma %PDF)», «Reintento… 200 sin duplicar», «Una sola fila VALIDADO»; `Reintentos_y_cargas_simultaneas_…`, `Cargas_simultaneas_de_PDF_distintos_…`, `Envios_simultaneos_publican_un_solo_mensaje`, `Un_fallo_del_almacenamiento_responde_503_…` |
| 4. Docker Compose desde cero: migraciones A/B, almacenamiento persistente, Gateway, RabbitMQ | E2E secciones 1, 2, 3, 3b y 6 |
| 5. Pruebas con SQL Server y RabbitMQ reales | `real-integracion-20261019.*` (15 pruebas) |
| 6. Caída y recuperación del bus, CorrelationId y recepción | E2E sección 7 (`docker compose stop/start rabbitmq`); `Caida_y_recuperacion_del_bus_…` |
| 7. CI y guía reproducible | `.github/workflows/ci.yml` y la guía de ejecución |

## Defecto encontrado por las pruebas reales

Las pruebas simuladas pasaban, pero con SQL Server real la prueba
`Reintentos_y_cargas_simultaneas_del_mismo_PDF_no_duplican` falló: de 5 cargas simultáneas del mismo PDF,
4 recibían **409** en lugar de **200**. Causa: cada carga renueva el sello de concurrencia del expediente y
las cargas simultáneas chocaban entre sí. Esto también rechazaba cargas legítimas de PDF distintos.
Se corrigió en `DocumentosController` (reintento controlado ante choques de concurrencia y respuesta
idempotente si otra carga guardó el mismo contenido), y se añadió la prueba
`Cargas_simultaneas_de_PDF_distintos_se_aceptan_todas`.

## Observación para el Sprint 2

El servicio gRPC de reglas no exige autenticación y `docker-compose.yml` publica su puerto en el equipo
anfitrión (`5083:8081`). Para el Sprint 1 sirve como herramienta de verificación, pero cuando el
Microservicio C lo consuma conviene dejarlo solo en la red interna de Docker (quitar el `ports`) o
protegerlo con un token de servicio.

---

## Validación funcional y normativa · 20 de octubre de 2026 (Paul Acurio)

Corrección de la regresión post-correcciones y flujo de caída/recuperación del bus:

| Tipo | Suite / corrida | Resultado | Archivo |
|---|---|---|---|
| **Simulada** | `Siha.MsA.Tests` | **58/58** (+5 `CatalogoImportacionRealTests` con PDF reales) | `simuladas-msa-20261020.trx` |
| **Simulada** | `Siha.MsB.Tests` | **60/60** | `simuladas-msb-20261020.trx` |
| **Real (Newman)** | Carpetas 00–04 | **75 requests · 132/132 aserciones** | `newman-carpetas00-04-20261020.log/.json` |
| **Real (Newman)** | PT-24 Fase A · RabbitMQ **caído** | **8 requests · 15/15 aserciones** (202 recuperable, CorrelationId conservado) | `newman-pt24-faseA-busdetenido-20261020.log/.json` |
| **Real (Newman)** | PT-24 Fase B · RabbitMQ **operativo** | **13 requests · 24/24 aserciones** (republicado + PT-R01…R05) | `newman-pt24-faseB-buslevantado-20261020.log/.json` |
| **Real (auditoría)** | Gateway 404/502/recuperación · estado H-01…H-06 | verificado | `gateway-audit-s1p08-20261020.log`, `hallazgos-h01-h06-estado-20261020.log`, `h01-rbac-gateway-20261020.log` |

Correcciones aplicadas a la colección: el script del `prefijo` (caso que fallaba en la corrida del 19)
y el caso **PT-15/H-01** (actualizado a esperar `200`, hallazgo corregido) — la regresión cerró en
**132/132**.

**Informe consolidado (8 tareas, criterios de aceptación y estado de tareas):**
`validacion-funcional-sprint1-paul-acurio.md` · Conciliación de catálogo:
`catalogo-planes-analiticos-20261020.md` (brecha: 19 planes de Software frente a los 43 oficiales).
