# Siha.BusVerifier · Suscriptor de prueba del bus

**Proyecto:** SIHA-IA · **Sprint:** 1 · **Tarea:** S1-P04 (PO Proxy & QA/AI Lead — Paul Medardo Acurio López)

Aplicación de consola **.NET 8** que se conecta a **RabbitMQ**, se suscribe a
`SolicitudCreadaEvent` (`Siha.Contracts`) e **imprime en pantalla el mensaje completo**: el cuerpo JSON
del contrato, los metadatos de transporte de MassTransit y las cabeceras, con el `CorrelationId`
destacado.

Sirve como **evidencia ejecutable del contrato** (S1-P01) y del flujo asíncrono B → RabbitMQ → C
(US-06). No forma parte de la solución de despliegue: es una herramienta de verificación para QA.

---

## 1. Requisitos

- .NET SDK 8.0
- RabbitMQ disponible (por defecto `rabbitmq://localhost:5672`)

```powershell
# Forma recomendada: solo el bus
docker compose up -d rabbitmq
docker compose ps rabbitmq        # debe verse como healthy

# Alternativa: toda la infraestructura del Sprint 1
docker compose up -d
```

| Dato | Valor por defecto | Origen |
|---|---|---|
| URI del bus | `rabbitmq://localhost` | `--uri` · `SIHA_RABBITMQ_URI` |
| Usuario | `siha` | `--usuario` · `SIHA_RABBITMQ_USUARIO` |
| Contraseña | `<SIHA_RABBITMQ_PASSWORD>` | `--password` · `SIHA_RABBITMQ_PASSWORD` |
| Cola de suscripción | `siha-verificador-solicitud-creada` | `--cola` · `SIHA_RABBITMQ_COLA` |
| Panel de administración | http://localhost:15672 | `siha` / `<SIHA_RABBITMQ_PASSWORD>` |

Los valores por defecto coinciden con `docker-compose.yml`, de modo que **no hace falta configurar
nada** en el entorno de desarrollo del Sprint 1.

---

## 2. Compilar y ejecutar

```powershell
# Compilar (verificación del entregable)
dotnet build tools/Siha.BusVerifier/Siha.BusVerifier.csproj

# Ejecutar (queda a la escucha hasta Ctrl+C)
dotnet run --project tools/Siha.BusVerifier
```

Salida esperada al arrancar:

```
======================================================================================
  SIHA-IA · Subscriptor de prueba del bus (Sprint 1 · tarea S1-P04)
  Universidad Técnica de Ambato · FISEI · Metodologías Ágiles
======================================================================================
  Contrato  : SolicitudCreadaEvent (Siha.Contracts)
  Host bus  : rabbitmq://localhost
  Usuario   : siha
  Cola      : siha-verificador-solicitud-creada
  Modo      : suscripción continua
--------------------------------------------------------------------------------------

Comprobando disponibilidad de RabbitMQ en rabbitmq://localhost ...
    · intento 1: conexión TCP establecida con localhost:5672.
RabbitMQ disponible. Iniciando el bus de MassTransit ...
✔ Bus iniciado. Suscripción activa:
    Host     : rabbitmq://localhost
    Cola     : siha-verificador-solicitud-creada
    Usuario  : siha
    Mensaje  : Siha.Contracts.Events.SolicitudCreadaEvent

Esperando mensajes del Microservicio B. Presione Ctrl+C para salir.
```

Si RabbitMQ no está disponible, el verificador **no falla de forma opaca**: informa el diagnóstico y
las instrucciones para levantar el bus, y termina con código 0.

---

## 3. Opciones de línea de comandos

| Opción | Descripción | Por defecto |
|---|---|---|
| `--uri <uri>` | URI del host RabbitMQ | `rabbitmq://localhost` |
| `--usuario <usuario>` | Usuario del bus | `siha` |
| `--password <clave>` | Contraseña del bus | `<SIHA_RABBITMQ_PASSWORD>` |
| `--cola <nombre>` | Cola de suscripción del verificador | `siha-verificador-solicitud-creada` |
| `--prefetch <n>` | Mensajes en vuelo por consumidor | `16` |
| `--timeout-conexion <seg>` | Segundos de espera a que el bus acepte la conexión | `15` |
| `--prueba` | Publica un evento sintético y termina al recibirlo (**autoprueba**) | desactivado |
| `--ayuda`, `-h`, `--help` | Muestra la ayuda | — |

Ejemplos:

```powershell
# Ayuda
dotnet run --project tools/Siha.BusVerifier -- --ayuda

# Bus en otro host/puerto
dotnet run --project tools/Siha.BusVerifier -- --uri rabbitmq://192.168.1.50:5672 --usuario qa --password qa-2026

# Autoprueba: no requiere que el Microservicio B esté levantado.
# Publica un SolicitudCreadaEvent sintético, comprueba que el contrato viaja íntegro
# y termina con código 0 (útil en CI o para validar credenciales del bus).
# No demuestra almacenamiento/validación de PDF por B ni recepción por C.
dotnet run --project tools/Siha.BusVerifier -- --prueba
```

Códigos de salida: `0` correcto (incluida la autoprueba y el caso «bus no disponible»),
`1` configuración inválida, `2` no se pudo iniciar el bus o no llegó el evento de la autoprueba.

---

## 4. Uso como evidencia del contrato (flujo real)

Terminal 1 — verificador a la escucha:

```powershell
dotnet run --project tools/Siha.BusVerifier
```

Terminal 2 — borrador → carga → envío contra el API Gateway (PowerShell 7):

```powershell
$login = Invoke-RestMethod -Method Post -Uri http://localhost:5000/api/auth/login `
  -ContentType "application/json" `
  -Body '{"correoInstitucional":"admin@uta.edu.ec","password":"<SIHA_ADMIN_PASSWORD>"}'
$headers = @{ Authorization = "Bearer $($login.token)" }

$resp = Invoke-WebRequest -Method Post -Uri http://localhost:5000/api/expedientes -Headers $headers `
  -ContentType "application/json" -Body '{
    "tipoTramite":"INTERNO","rutaNormativa":"CAU-P-011",
    "carreraOrigenCodigo":"SW","carreraDestinoCodigo":"TI","institucionOrigen":"UTA",
    "asignaturas":[{"codigoOrigen":"SW101","nombreOrigen":"Fundamentos de Programación","codigoDestino":"TI101","nota":8.5,"fechaAprobacionCertificada":"2025-07-15"}]}'

$resp.StatusCode # 201 Created, sin evento
$borrador = $resp.Content | ConvertFrom-Json
$documento = Invoke-RestMethod -Method Post -Uri "http://localhost:5000/api/expedientes/$($borrador.id)/documentos" `
  -Headers $headers -Form @{ archivo = Get-Item './docs/postman/muestras/record-academico.pdf' }
$envio = Invoke-RestMethod -Method Post -Uri "http://localhost:5000/api/expedientes/$($borrador.id)/enviar" -Headers $headers
$envio # 202 no prueba recepción; publicado=false indica fallo del bus
```

**Verificar recepción**, no solo HTTP: esperar el evento en esta consola independiente y comparar `ExpedienteId=$borrador.id`, `CorrelationId=$borrador.correlationId`, `MessageId=$borrador.id`, `Version=1.1` y las referencias `Documentos` (ID/ruta/hash/tamaño) con la carga y DB B/disco. El verificador no acredita consumo por C.

Si falta un PDF previamente validado o cambia tamaño/hash, B devuelve **422** y no publica ni siquiera el subconjunto que alcance el mínimo. Queda `NO_DISPONIBLE`; recargar el mismo contenido y usar `/enviar`. Reintentar sin corregir sigue bloqueado. `RECHAZADO`/`RETIRADO` no se incluyen ni bloquean.

**Coordinación pendiente con Sebastián y Alex:** no hay canal en esta sesión, no se realizó contacto ni se obtuvo aceptación. El acceso autorizado de C al binario sigue pendiente: `RutaAlmacenamiento` no es URL pública ni permiso de acceso; no se añade endpoint de descarga. La regresión real nueva está preparada, **no ejecutada por Docker no disponible**; pruebas InMemory/Moq se etiquetan simuladas.

El siguiente es un **extracto histórico v1.0**, no evidencia del nuevo flujo v1.1 ni de esta revisión. Para el contrato vigente, ver `docs/contrato-solicitud-creada-event.md`:

```
======================================================================================
  SolicitudCreadaEvent RECIBIDO · 2026-10-18 15:07:40.780 UTC
======================================================================================
--- Resumen del contrato ------------------------------------------------
  CodigoExpediente   : EXP-PRUEBA-20261018150740
  ExpedienteId       : af479a75-19a5-4d09-826a-ba4c4f0999eb
  CorrelationId      : 685ab668-2654-4198-b55b-96a040286170
  TipoTramite        : INTERNO
  RutaNormativa      : CAU-P-011
  CarreraOrigen      : SW
  CarreraDestino     : TI
  Estado             : PENDIENTE_CONTENIDOS
  Version            : 1.0
  FechaCreacionUtc   : 2026-10-18T15:07:40.5724849+00:00
  DocumentoIds       : 2 referencia(s) (sin binarios adjuntos)
      · 3af55f0b-ac96-4baa-abb5-b8d4d36ece92
      · 7e6149c7-99be-47c5-afc6-8aa374a80683
  AsignaturasOrigen  : 2 código(s)
      · SW101
      · SW201

--- Cuerpo JSON del mensaje ---------------------------------------------
{
  "ExpedienteId": "af479a75-19a5-4d09-826a-ba4c4f0999eb",
  "CorrelationId": "685ab668-2654-4198-b55b-96a040286170",
  "CodigoExpediente": "EXP-PRUEBA-20261018150740",
  "TipoTramite": "INTERNO",
  "RutaNormativa": "CAU-P-011",
  "CarreraOrigenCodigo": "SW",
  "CarreraDestinoCodigo": "TI",
  "DocumentoIds": [ "3af55f0b-…", "7e6149c7-…" ],
  "AsignaturasOrigen": [ "SW101", "SW201" ],
  "Estado": "PENDIENTE_CONTENIDOS",
  "FechaCreacionUtc": "2026-10-18T15:07:40.5724849+00:00",
  "Version": "1.0"
}

--- Metadatos de transporte (MassTransit / RabbitMQ) --------------------
  MessageId          : 80370000-d376-d85e-53b1-08df2d298dd0
  CorrelationId      : 685ab668-2654-4198-b55b-96a040286170
  ConversationId     : 80370000-d376-d85e-8861-08df2d298dd4
  Cola de entrada    : rabbitmq://localhost/siha-verificador-solicitud-creada
  Reintento          : 0

  ✔ Contrato verificado: el evento no adjunta archivos binarios, solo 2 referencia(s).
```

> El bloque «Cuerpo JSON del mensaje» se imprime con las **propiedades del record**; en el cable el
> envelope de MassTransit usa **camelCase** (`documentoIds`, `asignaturasOrigen`…) y
> `Content-Type: application/vnd.masstransit+json`. Ver §5 de
> `docs/contrato-solicitud-creada-event.md`.

---

## 5. Verificación en el panel de RabbitMQ

1. Abrir http://localhost:15672 con `siha` / `<SIHA_RABBITMQ_PASSWORD>`.
2. **Queues** → `siha-verificador-solicitud-creada`: aparece con **1 consumidor** mientras el
   verificador está en ejecución.
3. **Exchanges** → `Siha.Contracts.Events:SolicitudCreadaEvent` (tipo *fanout*, durable): es el
   exchange de publicación del contrato y tiene como *bindings* la cola del verificador y la del
   Microservicio C.
4. Para inspeccionar el mensaje en bruto: *Queues* → seleccionar la cola → **Get messages**
   (`Ack Mode: Nack message requeue true` para no consumirlo).
   Se verifica `content_type = application/vnd.masstransit+json` y `correlation_id` con el UUID del
   expediente.

---

## 6. Diferencias con el consumidor de referencia del Microservicio B

| Elemento | `tools/Siha.BusVerifier` (este proyecto) | `Siha.MsB.Api/Consumers/VerificacionSolicitudConsumer` |
|---|---|---|
| Rol | Herramienta de **QA externa** (S1-P04) | Consumidor **interno** de referencia (S1-J09) |
| Host | Proceso independiente, sin base de datos | Dentro del Microservicio B |
| Salida | Evento **completo** (contrato + metadatos) en consola | Log resumido de una línea |
| Dependencias | `Siha.Contracts` + `MassTransit.RabbitMQ` | MS B completo |
| Uso | Evidencia de contrato, autoprueba del bus, diagnóstico | Traza operativa de B |

Ninguno de los dos implementa aún el análisis semántico: esa es la responsabilidad del Microservicio C
en el Sprint 2/3, que deberá respetar este mismo contrato.

---

## 7. Estructura del proyecto

```
tools/Siha.BusVerifier/
├─ Siha.BusVerifier.csproj              # net8.0 + MassTransit.RabbitMQ 8.2.5 + Siha.Contracts
├─ Program.cs                           # arranque, comprobación del bus, autoprueba, cierre
├─ OpcionesBus.cs                       # opciones (CLI → variables de entorno → valores por defecto)
├─ VerificadorSolicitudCreadaConsumer.cs# IConsumer<SolicitudCreadaEvent>: imprime el evento completo
├─ NotificadorRecepcion.cs              # enganche para finalizar la autoprueba al recibir el evento
└─ README.md                            # este documento
```

---

## 8. Solución de problemas

| Síntoma | Causa probable | Acción |
|---|---|---|
| «`localhost:5672` no responde» | RabbitMQ apagado | `docker compose up -d rabbitmq` y esperar el estado *healthy* |
| «`ACCESS_REFUSED - Login was refused`» | Usuario/contraseña distintos del `docker-compose.yml` | Revisar `--usuario`/`--password` o las variables `SIHA_RABBITMQ_*` |
| La autoprueba no recibe el evento | Cola con nombre ocupado por otro consumidor o broker saturado | Usar `--cola` con un nombre alterno y revisar la cola en el panel |
| Se reciben eventos de una prueba anterior | Cola *durable* con mensajes acumulados | Purgar la cola desde el panel (*Queues → Purge*) o cambiar `--cola` |
| «No se pudo iniciar el bus» (código 2) | Credenciales o *vhost* inválidos | Verificar `RabbitMq:Usuario`/`Password` de `Siha.MsB.Api/appsettings.json` |

---

## 9. Criterio de aceptación de la tarea S1-P04

- [x] Proyecto de consola .NET 8 en `tools/Siha.BusVerifier/` con `Siha.Contracts` y `MassTransit.RabbitMQ`.
- [x] Se conecta a `rabbitmq://localhost` con `siha` / `<SIHA_RABBITMQ_PASSWORD>` (ambos configurables por CLI y variables de entorno).
- [x] Se suscribe a `SolicitudCreadaEvent` e imprime el mensaje completo (contrato + metadatos + `CorrelationId`).
- [x] `dotnet build tools/Siha.BusVerifier/Siha.BusVerifier.csproj` compila sin errores ni advertencias.
- [x] `dotnet run --project tools/Siha.BusVerifier -- --prueba` arranca, se suscribe, recibe y verifica el contrato (código 0).
- [x] Documentación de ejecución, verificación y solución de problemas (este README).
