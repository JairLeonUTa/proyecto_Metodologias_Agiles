# Evidencias de ejecución · Documentos, infraestructura y QA técnico

**Fecha de ejecución:** 19 de octubre de 2026 · **Responsable:** Sebastián Miranda
**Entorno:** Linux x86_64 · Docker 29.9.1 · Docker Compose 5.6.0 · .NET SDK 8.0 · Node 20
**Cómo reproducir:** `docs/guia-ejecucion-documentos-e-infraestructura.md`

## Resumen

| Tipo | Suite | Infraestructura usada | Resultado | Archivo |
|---|---|---|---|---|
| **Simulada** | `Siha.MsA.Tests` | EF Core InMemory, mocks | **53/53** | `simuladas-msa-20261019.trx` |
| **Simulada** | `Siha.MsB.Tests` | EF Core InMemory, Moq, arnés en memoria de MassTransit, inspección de configuración | **60/60** | `simuladas-msb-20261019.trx` |
| **Real** | `Siha.Integracion.Tests` | SQL Server 2022 + RabbitMQ 3.13 en contenedores (Testcontainers), disco real | **8/8** | `real-integracion-20261019.trx` y `.log` |
| **Real (E2E)** | `scripts/e2e-docker-compose.sh` | Pila completa de `docker-compose.yml` desde cero (`down -v` + `up`) | **28/28** | `e2e-compose-20261019-233419.md` y `.log` |
| **Real (API)** | Colección Postman (Newman) contra la pila de Compose | Gateway + MsA + MsB + SQL + RabbitMQ | 156/159 aserciones; **carpetas 04 y 05 (expedientes y bus) sin fallos** | `real-postman-newman-20261019.log` |

Las 3 aserciones fallidas de Newman pertenecen a la carpeta **03 · Catálogo** (fuera del alcance de
documentos e infraestructura) y se informan a su responsable:

1. «Crear carrera QA · Código en mayúsculas»: el test compara contra el texto literal `{{PREFIJO}}`
   (la variable no se reemplaza dentro del script de prueba).
2. y 3. «PT-15 · Hallazgo H-01»: el test espera **404** al importar por el Gateway, pero la ruta ya
   responde **200**. El hallazgo H-01 parece corregido; hay que actualizar el caso.

## Qué demuestra cada evidencia real

| Requisito del líder | Evidencia |
|---|---|
| 1. Flujo registrar → almacenar/validar PDF → publicar con requisitos | E2E filas «Registro del borrador», «Envío sin documentos bloqueado (422)», «Carga de PDF real», «Envío con documento verificado (202)»; integración `Recorrido_completo_…` |
| 2. Referencias al bus = documentos existentes y aceptados; sin confiar en el cliente | E2E «Rechazo de documentos declarados por el cliente (400)», «SHA-256 calculado por el servidor»; integración `No_se_publica_si_el_PDF_ya_no_existe_…` y aserciones sobre `Documentos` del mensaje real |
| 3. Cargas rechazadas, fallos de almacenamiento y reintentos sin duplicar | E2E «Carga rechazada (firma %PDF)», «Reintento… 200 sin duplicar», «Una sola fila VALIDADO»; integración `Reintentos_y_cargas_simultaneas_…`, `Cargas_simultaneas_de_PDF_distintos_…`, `Envios_simultaneos_publican_un_solo_mensaje`, `Un_fallo_del_almacenamiento_responde_503_…` |
| 4. Docker Compose desde cero: migraciones A/B, almacenamiento persistente, Gateway, RabbitMQ | E2E secciones 1, 2, 3 y 6 («PDF intacto tras recrear el contenedor ms-b») |
| 5. Pruebas con SQL Server y RabbitMQ reales | `real-integracion-20261019.*` (8 pruebas) |
| 6. Caída y recuperación del bus, CorrelationId y recepción | E2E sección 7 (`docker compose stop/start rabbitmq`); integración `Caida_y_recuperacion_del_bus_…` |
| 7. CI y guía reproducible | `.github/workflows/ci.yml` (jobs unitarias / integración / e2e) y la guía de ejecución |

## Defecto encontrado por las pruebas reales

Las pruebas simuladas pasaban, pero con SQL Server real la prueba
`Reintentos_y_cargas_simultaneas_del_mismo_PDF_no_duplican` falló: de 5 cargas simultáneas del mismo PDF,
4 recibían **409** en lugar de **200**. Causa: cada carga renueva el sello de concurrencia del expediente y
las cargas simultáneas chocaban entre sí. Esto también rechazaba cargas legítimas de PDF distintos.
Se corrigió en `DocumentosController` (reintento controlado ante choques de concurrencia y respuesta
idempotente si otra carga guardó el mismo contenido), y se añadió la prueba
`Cargas_simultaneas_de_PDF_distintos_se_aceptan_todas`.
