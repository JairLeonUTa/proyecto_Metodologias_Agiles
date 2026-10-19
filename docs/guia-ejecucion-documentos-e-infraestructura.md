# Guía de ejecución · Documentos, infraestructura y QA técnico (Sprint 1)

**Proyecto:** SIHA-IA · **Responsable:** Sebastián Miranda — Documentos, infraestructura y QA técnico
**Fecha:** 19 de octubre de 2026

Esta guía permite a cualquier integrante reproducir, desde cero, los resultados de la integración de
documentos e infraestructura: flujo de PDF, migraciones, Gateway, RabbitMQ, persistencia de archivos y
caída/recuperación del bus.

---

## 1. Flujo de documentos propuesto (para acordar con Alex y Jair)

> **Pendiente de aprobación:** este es el flujo implementado en MsB y en el frontend. Alex (frontend) y
> Jair (expedientes/bus) deben revisarlo y confirmarlo.

| Paso | Endpoint (vía Gateway) | Qué hace el servidor | Estado del expediente |
|---|---|---|---|
| 1. Registrar borrador | `POST /api/expedientes` | Crea el expediente con sus asignaturas. **No publica nada.** Si el cliente envía `documentos` (rutas/hashes/tamaños), responde **400**. | `PENDIENTE_DOCUMENTOS` |
| 2. Cargar cada PDF | `POST /api/expedientes/{id}/documentos` (multipart, campo `archivo`) | Valida extensión, MIME, tamaño y firma binaria `%PDF`; guarda con nombre GUID en el volumen `siha-archivos` (escritura atómica `.tmp` → rename); **calcula el SHA-256 en el servidor**. | `PENDIENTE_DOCUMENTOS` |
| (opcional) Retirar | `DELETE /api/expedientes/{id}/documentos/{docId}` | Marca `RETIRADO` y borra el archivo. Solo antes del envío. | `PENDIENTE_DOCUMENTOS` |
| 3. Enviar | `POST /api/expedientes/{id}/enviar` | Vuelve a leer **desde disco** cada documento `VALIDADO` (existe, tamaño y SHA-256 coinciden). Si hay al menos `Archivos:MinimoDocumentosRequeridos` (1 por defecto), reserva el expediente con concurrencia optimista y publica `SolicitudCreadaEvent` v1.1 **solo con referencias verificadas**. | `PENDIENTE_CONTENIDOS` (publicado) o `REGISTRADO_PENDIENTE_PUBLICACION` (bus caído) |
| 4. Recuperación | Automática cada `Republicacion:IntervaloSegundos` (10 s en Compose) o `POST /api/expedientes/{id}/republicar` | Reintenta conservando el **mismo CorrelationId**; vuelve a verificar los PDF. Un expediente ya publicado **nunca** se publica de nuevo. | `PENDIENTE_CONTENIDOS` |

**Respuestas relevantes**

| Situación | Respuesta |
|---|---|
| Archivo vacío, demasiado grande, sin extensión `.pdf`, MIME incorrecto o sin firma `%PDF` | **400**; el intento queda auditado como `RECHAZADO` en `EXPEDIENTE_DOC` |
| Mismo PDF cargado otra vez (reintento, doble clic o cargas simultáneas) | **200** con el documento existente: sin fila ni archivo duplicados (índice único filtrado `IX_EXPEDIENTE_DOC_Unico_Validado`) |
| Varios PDF distintos cargados a la vez | **201** para todos (los choques de concurrencia se reintentan internamente) |
| Disco/volumen no escribible | **503**; no queda fila ni archivo huérfano; el cliente puede reintentar |
| Enviar sin documentos válidos, o si un PDF desapareció o fue alterado en disco | **422** con el detalle; el documento afectado pasa a `NO_DISPONIBLE` y **no** se publica nada |
| Dos envíos simultáneos del mismo expediente | Uno publica (**202**), el otro recibe **409**; llega **un solo** mensaje |
| Segundo envío de un expediente ya publicado | **200**, sin volver a publicar |
| Bus caído al enviar | **202** con estado `REGISTRADO_PENDIENTE_PUBLICACION`; se recupera solo al volver RabbitMQ |
| Cargar documentos después de enviar | **409** |

El `MessageId` del mensaje es igual al `ExpedienteId`, para que el consumidor (Microservicio C) pueda
descartar un duplicado si lo hubiera. Ver `docs/contrato-solicitud-creada-event.md` (v1.1).

---

## 2. Requisitos

| Herramienta | Versión probada | Para qué |
|---|---|---|
| Docker Desktop (Windows/macOS) o Docker Engine (Linux) con Compose v2 | Docker 29.9, Compose 5.6 | Pruebas de integración y E2E |
| .NET SDK | 8.0 | Compilar y ejecutar pruebas |
| Node.js | 20 | Compilar el frontend |
| Git Bash (en Windows) | — | Ejecutar `scripts/e2e-docker-compose.sh` |
| Postman o `npx newman` | — | Colección `docs/postman` (opcional) |

> Docker Desktop debe estar **abierto** antes de ejecutar las pruebas de integración o el E2E.
> La primera ejecución descarga ~3 GB de imágenes (SQL Server 2022, RabbitMQ 3.13, .NET 8).

---

## 3. Niveles de prueba: simuladas vs. infraestructura real

| Nivel | Proyecto / script | Infraestructura | Pruebas | Comando |
|---|---|---|---|---|
| **Simuladas** | `tests/Siha.MsA.Tests` | EF Core InMemory, mocks | 53 | `dotnet test tests/Siha.MsA.Tests` |
| **Simuladas** | `tests/Siha.MsB.Tests` | EF Core InMemory, Moq, arnés en memoria de MassTransit, inspección de configuración | 60 | `dotnet test tests/Siha.MsB.Tests` |
| **Reales** | `tests/Siha.Integracion.Tests` | **SQL Server 2022 y RabbitMQ 3.13 reales** (Testcontainers), sistema de archivos real, suscriptor independiente, MsA y MsB completos | 15 | `dotnet test tests/Siha.Integracion.Tests` |
| **Reales (E2E)** | `scripts/e2e-docker-compose.sh` | **Toda la pila de `docker-compose.yml`** desde cero: SQL A/B, RabbitMQ, ms-a (REST + gRPC), ms-b, gateway, frontend | 31 comprobaciones | `bash scripts/e2e-docker-compose.sh` |

Pruebas con infraestructura real (`Siha.Integracion.Tests`).

`FlujoDocumentosRealTests` (documentos, expedientes y bus):

1. `Las_migraciones_de_A_y_B_se_aplican_en_SQL_Server_real`
2. `Recorrido_completo_registro_carga_envio_y_recepcion_en_RabbitMQ` (CorrelationId en cabecera y cuerpo, MessageId = ExpedienteId, referencias verificadas)
3. `Reintentos_y_cargas_simultaneas_del_mismo_PDF_no_duplican`
4. `Cargas_simultaneas_de_PDF_distintos_se_aceptan_todas`
5. `Envios_simultaneos_publican_un_solo_mensaje`
6. `No_se_publica_si_el_PDF_ya_no_existe_en_el_almacenamiento`
7. `Un_fallo_del_almacenamiento_responde_503_sin_dejar_filas`
8. `Caida_y_recuperacion_del_bus_conservan_el_CorrelationId_y_el_mensaje_llega` (detiene y reinicia el contenedor de RabbitMQ)

`ReglasGrpcRealTests` (servidor gRPC de reglas, S1-S04 / S1-S09):

9. `Grpc_devuelve_los_umbrales_y_reglas_sembrados_en_SQL_Server_igual_que_REST` (CAU-P-011 y CAU-P-122)
10. `Grpc_filtra_por_tipo_de_tramite`
11. `Grpc_rechaza_ruta_o_tipo_invalidos_con_InvalidArgument` (3 casos)
12. `Un_cambio_de_umbrales_por_REST_se_refleja_en_gRPC_y_persiste_en_SQL_Server`

---

## 4. Paso a paso

Todos los comandos se ejecutan desde la raíz del repositorio.

### 4.1 Compilar y pruebas simuladas

```bash
dotnet restore SIHA-IA.sln
dotnet build SIHA-IA.sln -c Release
dotnet test tests/Siha.MsA.Tests -c Release --no-build
dotnet test tests/Siha.MsB.Tests -c Release --no-build
```

Resultado esperado: `Passed: 53` y `Passed: 60`.

### 4.2 Pruebas con SQL Server y RabbitMQ reales

```bash
dotnet test tests/Siha.Integracion.Tests -c Release --logger "console;verbosity=normal"
```

Resultado esperado: `Passed: 15` (tarda ~4 minutos; Testcontainers crea y destruye los contenedores solo).

### 4.3 Docker Compose desde cero (E2E)

En **Git Bash** (Windows) o una terminal (Linux/macOS):

```bash
bash scripts/e2e-docker-compose.sh
```

El script ejecuta `docker compose down -v` (borra volúmenes), `up -d --build` y comprueba:

1. Arranque con volúmenes vacíos.
2. Migraciones reales de DB A y DB B (`__EFMigrationsHistory`) e índice único filtrado.
3. Login por el Gateway (JWT de MsA).
3b. Llamada **gRPC real** a `ms-a:8081` con `grpcurl` (imagen `fullstorydev/grpcurl`): umbrales iguales a los de REST y `InvalidArgument` ante una ruta inválida.
4. Flujo documental completo, rechazos, reintento sin duplicar y hash del archivo en el volumen.
5. Recepción del mensaje por el suscriptor con el mismo CorrelationId (exactamente uno).
6. El PDF sobrevive a recrear el contenedor `ms-b` (volumen persistente).
7. `docker compose stop rabbitmq` → envío queda recuperable → `start` → reintento automático → mensaje recibido con el **mismo** CorrelationId.
8. El frontend (Nginx) sirve la SPA y reenvía una carga de ~2 MB al Gateway.

Opciones: `CONSERVAR=1` deja los contenedores encendidos al terminar; `CONSTRUIR=0` reutiliza imágenes ya construidas.

La evidencia queda en `docs/evidencias/e2e-compose-<fecha>.md` (tabla de resultados) y `.log` (salida completa).

### 4.4 Prueba manual desde el navegador

```bash
CONSERVAR=1 bash scripts/e2e-docker-compose.sh   # o: docker compose up -d --build
```

1. Abrir <http://localhost:5173> e iniciar sesión (`admin@uta.edu.ec` / `Admin123*`).
2. **Registrar solicitud** → completar datos → adjuntar uno o más PDF → **Registrar y enviar solicitud**.
3. La pantalla muestra el expediente `PENDIENTE_CONTENIDOS` con su CorrelationId.
4. RabbitMQ: <http://localhost:15672> (`siha` / `siha-rabbit-2026`), intercambio `Siha.Contracts.Events:SolicitudCreadaEvent`.
5. Para probar la caída del bus: `docker compose stop rabbitmq`, enviar otra solicitud (queda «pendiente de publicación»), `docker compose start rabbitmq` y esperar ~10 s: pasa sola a `PENDIENTE_CONTENIDOS`.

### 4.5 Colección Postman

Importar `docs/postman/SIHA-IA-Sprint1.postman_collection.json`. Los PDF de prueba están en
`docs/postman/muestras/`. Con Newman, desde la raíz del repositorio:

```bash
npx newman run docs/postman/SIHA-IA-Sprint1.postman_collection.json --env-var baseUrl=http://localhost:5000
```

### 4.6 Smoke test en PowerShell (Windows)

```powershell
./scripts/smoke-sprint1.ps1
```

---

## 5. Integración continua

`.github/workflows/ci.yml` ejecuta en cada push:

| Job | Contenido | Tipo |
|---|---|---|
| `1 · Build + pruebas unitarias` | Restore, build, `Siha.MsA.Tests`, `Siha.MsB.Tests` | Simuladas |
| `1b · Build del frontend` | `npm ci` + `npm run build` (incluye `tsc`) | Compilación |
| `2 · Integración` | `Siha.Integracion.Tests` con Testcontainers | **Real** |
| `3 · E2E Docker Compose` | `scripts/e2e-docker-compose.sh` | **Real** |

Cada job publica sus resultados como artefacto (`resultados-unitarias`, `resultados-integracion-real`,
`evidencia-e2e-compose`), descargables desde la pestaña **Actions** de GitHub.

---

## 6. Problemas frecuentes

| Síntoma | Causa | Solución |
|---|---|---|
| `Docker is either not running or misconfigured` | Docker Desktop cerrado | Abrir Docker Desktop y esperar a que diga *Running* |
| Falla la sección 3b (gRPC) del E2E | No se pudo descargar `fullstorydev/grpcurl` | `docker pull fullstorydev/grpcurl:v1.9.1` y repetir |
| `toomanyrequests` / `429` al descargar `rabbitmq` | Límite de descargas anónimas de Docker Hub | `docker login` o esperar unos minutos |
| Puerto 1433, 5000, 5672 o 5173 ocupado | Otro SQL Server, RabbitMQ o servicio local | Detener el servicio local o cambiar el puerto en `docker-compose.override.yml` |
| `413 Request Entity Too Large` al subir un PDF | Imagen del frontend antigua | `docker compose build frontend` (Nginx ahora admite 12 MB) |
| El expediente queda en `REGISTRADO_PENDIENTE_PUBLICACION` | RabbitMQ caído | Levantar RabbitMQ; el reintento automático lo publica en ≤ 10 s |
| Envío responde 422 «no disponible» | El PDF se borró o cambió en el volumen | Volver a cargarlo y enviar de nuevo |
