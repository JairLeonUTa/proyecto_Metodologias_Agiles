# Plan de pruebas funcionales · Sprint 1 (US-01 a US-06)

**Proyecto:** SIHA-IA · Sistema Inteligente de Homologación Académica Asistido por Agentes IA
**Institución:** Universidad Técnica de Ambato — FISEI — Metodologías Ágiles
**Sprint:** 1 (Base del Sistema) · **Tareas:** S1-P05, S1-P06, S1-P07, S1-P09, S1-P10
**Autor:** Paul Medardo Acurio López — Product Owner Proxy & QA/AI Lead
**Versión:** 1.0 · **Fecha:** 18 de octubre de 2026

---

## 1. Objetivo y alcance

Verificar, con evidencia reproducible, que las historias **US-01 a US-06** del Sprint 1 cumplen sus
criterios de aceptación: autenticación y RBAC, reglas y umbrales θ/δ/τ, catálogo académico con la
importación de los **43 planes analíticos**, control de acceso en el borde, alta de expedientes
(internos y externos), contrato del evento en el bus, y **recuperación ante fallo de RabbitMQ**.

Incluye además la **batería de regresión funcional** que se repetirá al cierre de cada sprint (S1-P10).

**Fuera de alcance (Sprint 2/3):** análisis semántico del Microservicio C, dictamen del docente,
notificaciones, rendimiento sostenido y pruebas de penetración formales.

---

## 2. Niveles y estrategia de prueba

| Nivel | Qué cubre | Herramienta | Cuándo |
|---|---|---|---|
| **Unitaria** | Validación δ ≤ θ y rangos; reglas por reflexión de `[Authorize]`; hashing BCrypt; JWT; importación (duplicados, 48 h, planes incompletos) | `dotnet test tests/Siha.MsA.Tests` | En cada cambio (bloqueante en CI) |
| **Funcional de API** | Flujos HTTP completos por el punto de entrada único, incluidos 400/401/403/404/409 | Colección Postman (`docs/postman/`) o Swagger | Por historia terminada |
| **Integración de bus** | `SolicitudCreadaEvent` publicado y consumido; contrato sin binarios | `tools/Siha.BusVerifier` + panel RabbitMQ | Por historia US-06 y ante cambios del contrato |
| **Recuperación** | Fallo de RabbitMQ, estado `REGISTRADO_PENDIENTE_PUBLICACION`, republicación con el mismo `CorrelationId` | Postman + `docker compose stop rabbitmq` | Al cierre del Sprint 1 y en cada regresión |
| **Regresión** | US-01 a US-06 completas tras corregir un hallazgo | Postman (carpeta de regresión) + `dotnet test` | Antes de cada entrega |

**Regla de oro:** un caso se considera **aprobado** solo si existe evidencia adjunta (captura o salida
de consola) y el resultado coincide con el esperado. Sin evidencia, el caso queda **bloqueado**.

---

## 3. Entorno de pruebas y datos

| Elemento | Valor |
|---|---|
| Punto de entrada | `http://localhost:5000` (API Gateway Ocelot) |
| Microservicio A (directo) | `http://localhost:5081/swagger` |
| Microservicio B (directo) | `http://localhost:5082/swagger` |
| RabbitMQ (panel) | `http://localhost:15672` · `siha` / `<SIHA_RABBITMQ_PASSWORD>` |
| SQL Server A / B | `localhost,1433` · `localhost,14330` · `sa` / `<SIHA_SQL_PASSWORD>` |
| Cuenta administradora (semilla) | `admin@uta.edu.ec` / `<SIHA_ADMIN_PASSWORD>` |
| Cuentas de apoyo | `operador.qa@uta.edu.ec` / `Operador123*` (OPERADOR) y `docente.qa@uta.edu.ec` / `Docente123*` (DOCENTE) — se crean en PT-03 |
| Datos semilla | 3 roles; umbrales CAU-P-011 (80/10/70) y CAU-P-122 (80/15/65); reglas RN-01…RN-05; 5 carreras; malla SW 2024-A con SW101/SW201/SW301 |
| Colección de pruebas | `docs/postman/SIHA-IA-Sprint1.postman_collection.json` |

Preparación del entorno (una sola vez por sesión):

```powershell
docker compose up -d --build
docker compose ps                       # todos los servicios "healthy"/"running"
# Verificación rápida
Invoke-RestMethod http://localhost:5000/health
Invoke-RestMethod http://localhost:5082/health
```

Ejecución de la colección con **Newman** (opcional, para evidencia en CI):

```powershell
npm install -g newman
newman run docs/postman/SIHA-IA-Sprint1.postman_collection.json `
  --env-var "baseUrl=http://localhost:5000" `
  --reporters cli,htmlextra
```

---

## 4. Casos de prueba por historia

Notación: **CA** = criterio de aceptación de la historia · **Evidencia** = artefacto que se adjunta al
informe de QA.

### 4.1 US-01 · Autenticación, roles y seguridad (PT-01 a PT-05)

| ID | Caso | Precondiciones | Pasos (resumen) | Resultado esperado | CA | Evidencia |
|---|---|---|---|---|---|---|
| **PT-01** | Login exitoso y emisión del JWT | Servicios arriba; cuenta admin sembrada | 1) `POST /api/auth/login` con `admin@uta.edu.ec` / `<SIHA_ADMIN_PASSWORD>` | `200 OK` con `token`, `expiraUtc` y `usuario.rol = ADMINISTRADOR`; el token se captura en la colección | US-01/CA2 | Captura de Postman (respuesta + test *Login administrador*) |
| **PT-02** | Login rechazado | Igual que PT-01 | 1) Login con contraseña errónea; 2) login con correo inexistente | `401 Unauthorized` — «Credenciales inválidas o cuenta inactiva.» en ambos casos | US-01/CA2 | Captura `401` + assert de la colección |
| **PT-03** | Alta de usuarios y asignación de rol | Token de admin | 1) `POST /api/usuarios` rol `OPERADOR`; 2) repetir con `DOCENTE`; 3) repetir el primero; 4) `POST` con `rol = INVITADO`; 5) `POST` con contraseña de 5 caracteres | `201 Created` (1 y 2), `409 Conflict` (3), `400 Bad Request` (4) con roles permitidos, `400 Bad Request` (5) | US-01/CA1, CA3 | Capturas `201`, `409`, `400` + listado `GET /api/usuarios` |
| **PT-04** | RBAC de cuentas y roles | Tokens de OPERADOR y DOCENTE | 1) `GET /api/usuarios` con OPERADOR; 2) `GET /api/roles` con DOCENTE; 3) `GET /api/usuarios` sin token; 4) `GET /api/usuarios` con admin | `403`, `403`, `401`, `200` respectivamente | US-01/CA3, US-04/CA2 | Capturas `403` y `401` |
| **PT-05** | El hash nunca se expone y la cuenta inactiva no accede | Token de admin | 1) Inspeccionar la respuesta de `GET /api/usuarios` buscando `passwordHash`; 2) `PATCH /api/usuarios/{id}/estado` `{activo:false}`; 3) login de esa cuenta; 4) reactivarla | 1) No existe el campo; 3) `401 Unauthorized`; 4) `200 OK` | US-01/CA1, CA2 | Captura del JSON del usuario + `401` |

### 4.2 US-02 · Reglas y umbrales θ, δ, τ (PT-06 a PT-09)

| ID | Caso | Precondiciones | Pasos (resumen) | Resultado esperado | CA | Evidencia |
|---|---|---|---|---|---|---|
| **PT-06** | Consulta de reglas y parámetros | Token de cualquier rol | 1) `GET /api/reglas`; 2) `GET /api/reglas?rutaNormativa=CAU-P-122&tipoTramite=EXTERNO`; 3) `GET /api/reglas/parametros/CAU-P-011` | `200 OK`; RN-01…RN-05 con tipo, versión, fuente y vigencia; parámetros 80/10/70 versión 1 | US-02/CA1 | Capturas de las tres respuestas |
| **PT-07** | Actualización válida de umbrales | Token de admin | 1) `PUT /api/reglas/parametros/CAU-P-011` con `{theta:82,delta:12,tau:71}`; 2) `GET` del mismo recurso | `200 OK` con `version = 2`; la consulta devuelve los nuevos valores | US-02/CA2 | Captura `PUT` + `GET` |
| **PT-08** | Umbrales inválidos sin mutación | Token de admin; parámetros en 82/12/71 (tras PT-07) | 1) `PUT` con `{theta:50,delta:80,tau:60}` (δ > θ); 2) `PUT` con `{theta:-1,...}`; 3) `PUT` con `{theta:101,...}`; 4) `PUT` con `{theta:50,delta:10,tau:120}`; 5) `GET` de verificación | Cada `PUT` responde `400 Bad Request` con el mensaje de la regla; el `GET` final conserva **82/12/71 versión 2** (sin mutación) | US-02/CA2 | Capturas de los cuatro `400` + `GET` final |
| **PT-09** | RBAC de reglas | Tokens OPERADOR y DOCENTE | 1) `PUT` parámetros con OPERADOR; 2) `POST /api/reglas` con DOCENTE; 3) `PUT` sin token; 4) `GET /api/reglas/parametros/CAU-P-999` | `403`, `403`, `401`, `404` respectivamente | US-02/CA3 | Capturas `403`/`401`/`404` |

### 4.3 US-03 · Catálogo académico e importación de los 43 planes (PT-10 a PT-15)

**PT-10 · CRUD de catálogo y conflictos**

| Paso | Acción | Esperado |
|---|---|---|
| 1 | `GET /api/carreras` | `200 OK`; al menos 5 carreras de la semilla (SW, TI, IND, ROB, TEL) |
| 2 | `POST /api/carreras` `{codigo:"QA1", nombre:"Carrera QA"}` | `201 Created` |
| 3 | Repetir el paso 2 | `409 Conflict` — «Ya existe una carrera con el código QA1.» |
| 4 | `GET /api/carreras/999999` | `404 Not Found` |
| 5 | `POST /api/mallas` con la carrera del paso 2 (`version "2026-QA"`, `anio 2026`) | `201 Created`; se captura `mallaId` |
| 6 | Repetir el paso 5 | `409 Conflict` — versión duplicada |
| 7 | `POST /api/mallas` con `carreraId = 999999` | `400 Bad Request` — «La carrera 999999 no existe.» |
| 8 | `POST /api/carreras` con el token de OPERADOR | `403 Forbidden` |
| **CA cubiertos** | US-03/CA1 (consulta de carrera, versión y asignatura), US-03/CA2 | **Evidencia**: capturas de `201`, `409`, `400`, `403` |

**PT-11 · Importación de los 43 planes analíticos (caso feliz)**

- **Precondición:** `mallaId` obtenido en PT-10 (o malla SW 2024-A), token de admin, catálogo base sin los códigos a importar.
- **Datos:** lote JSON con los **43 planes** de la carrera, cada uno con `codigo`, `nombre`, `creditos`, `horas = creditos × 48` y al menos dos unidades temáticas cuya suma de horas ≤ `horas` declaradas.
- **Pasos:** 1) `POST /api/asignaturas/importar` con el lote; 2) `GET /api/asignaturas?mallaId={id}`; 3) contar las asignaturas con `planCompleto = true`.
- **Resultado esperado:** `200 OK`; `total = 43`, `importadas = 43`, `rechazadas = 0`, `errores = []`; las 43 asignaturas quedan con `planCompleto = true` y visibles en la consulta.
- **CA:** US-03/CA1, CA3 (los planes completos pasan a estar listos para el análisis semántico).
- **Evidencia:** captura del `200` con el `importacionResultado` y del `GET` con el conteo; **captura de Swagger** del endpoint (`/api/asignaturas/importar`).

**PT-12 · Importación con códigos duplicados**

- **Pasos:** 1) importar un lote con `SW101` (ya existente en la malla) y un `ADM-QA-01` nuevo; 2) importar un lote cuyo propio contenido repite `ADM-QA-01` dos veces.
- **Esperado:** `200 OK` en ambos; `rechazadas ≥ 1` y `errores` con «Código duplicado en la malla 2024-A: SW101» / «Código duplicado en la malla {version}: ADM-QA-01»; las asignaturas válidas del lote sí se importan (el proceso no aborta).
- **CA:** US-03/CA3 · **Evidencia:** JSON de `importacionResultado` con `rechazadas` y el arreglo `errores` + captura de `tests` unitarios (`CatalogoServiceTests.Rechaza_codigos_duplicados_en_la_misma_malla`).

**PT-13 · Equivalencia 48 h por crédito UTA**

- **Pasos:** 1) importar un plan con `creditos = 4` y `horas = 100` (esperado 192 h); 2) importar el mismo plan corregido con 192 h y dos unidades de 96 h.
- **Esperado:** caso 1 → `200 OK`, asignatura importada con `planCompleto = false` y error «no cumple la equivalencia de 48 h/crédito UTA (esperado 192 h, recibido 100 h); queda pendiente»; caso 2 → `planCompleto = true`.
- **CA:** US-03/CA3 · **Evidencia:** capturas de ambos resultados + prueba unitaria `Marca_pendiente_el_plan_que_no_cumple_48h_por_credito`.

**PT-14 · Planes incompletos (no deben pasar al análisis semántico)**

| Subcaso | Entrada | Esperado |
|---|---|---|
| PT-14a | Plan sin unidades temáticas (`unidades: []`) | Importado con `planCompleto = false`; error «no tiene unidades temáticas; queda pendiente» |
| PT-14b | Plan con `creditos = 0` | `planCompleto = false`; «no declara créditos válidos» |
| PT-14c | Plan cuyas unidades suman más horas que las declaradas | `planCompleto = false`; «suma N h en unidades y supera las M h declaradas» |
| PT-14d | Plan con número de unidad repetido | `planCompleto = false`; «repite el número de unidad» |
| PT-14e | Plan con `codigo` o `nombre` vacío | `rechazadas +1`; «el código y el nombre son obligatorios» |
| PT-14f | Lote vacío `asignaturas: []` | `400 Bad Request` — «Debe enviar al menos una asignatura para la importación.» |
| PT-14g | `mallaId` inexistente | `400 Bad Request` — «La malla 999 no existe en el catálogo.» |

- **CA:** US-03/CA3 · **Evidencia:** capturas de cada subcaso con su `planCompleto` y su mensaje.

**PT-15 · Importación a través del API Gateway (hallazgo H-01)**

- **Pasos:** 1) `POST http://localhost:5000/api/asignaturas/importar` con un lote mínimo; 2) repetir contra `http://localhost:5081/api/asignaturas/importar` con el mismo token.
- **Resultado esperado (diseño):** `200 OK` en el punto de entrada único.
- **Resultado observado/esperado por configuración:** el Gateway **no publica** ese `POST` (la plantilla `/api/asignaturas/{everything}` solo admite `GET`/`PUT`), por lo que devuelve **`404`**; la llamada directa al Microservicio A responde `200` (ver H-01 de `matriz-rbac.md`).
- **Tratamiento de QA:** caso **BLOQUEADO** para el borde hasta que se corrija la ruta Ocelot; se documenta el defecto con severidad **Alta** (impide la carga masiva por el punto de entrada único) y se adjunta la evidencia de ambos intentos.

### 4.4 US-04 · Control de acceso en el borde (PT-16 a PT-18)

| ID | Caso | Pasos | Esperado | CA | Evidencia |
|---|---|---|---|---|---|
| **PT-16** | Sin token ⇒ 401 | `GET /api/usuarios`, `GET /api/expedientes`, `POST /api/expedientes` sin cabecera `Authorization` | `401 Unauthorized` en los tres | US-04/CA1 | Capturas `401` |
| **PT-17** | Token con rol no autorizado ⇒ 403 | `POST /api/carreras` y `GET /api/usuarios` con token de OPERADOR | `403 Forbidden` en ambos | US-04/CA2 | Capturas `403` |
| **PT-18** | Ruta no publicada ⇒ 404 | `GET /api/noexiste` y `POST /api/asignaturas/importar` (hallazgo H-01) | `404 Not Found` controlado, sin *stack trace* | US-04/CA3 | Capturas `404` |
| **PT-18b** | Token manipulado | JWT con la firma alterada o vencido | `401 Unauthorized` | US-04/CA1 | Captura `401` |

### 4.5 US-05 · Alta y consulta de expedientes (PT-19 a PT-22)

**PT-19 · Expediente interno con ruta CAU-P-011 (cambio de carrera)**

- **Precondición:** bus arriba; `tools/Siha.BusVerifier` en ejecución (evidencia del contrato); token de OPERADOR (o admin).
- **Pasos:**
  1. `POST /api/expedientes` con `tipoTramite: "INTERNO"`, `rutaNormativa: "CAU-P-011"`, `carreraOrigenCodigo: "SW"`, `carreraDestinoCodigo: "TI"`, `institucionOrigen: "UTA"`, un documento de referencia (`record.pdf`, `/data/record.pdf`, `hashSha256`, `tamanoBytes`) y una asignatura (`SW101` → `TI101`, nota 18).
  2. Anotar `id`, `codigo`, `estado`, `correlationId` y `publicado` de la respuesta.
  3. `GET /api/expedientes/{id}`.
- **Esperado:** paso 1 → **`202 Accepted`** con `estado = PENDIENTE_CONTENIDOS`, `publicado = true` y `correlationId` no vacío; paso 3 → `200 OK` con `eventoPublicado = true`, `intentosPublicacion = 1`, el documento y la asignatura persistidos.
- **CA:** US-05/CA1, CA3; US-06/CA2 · **Evidencia:** capturas `202` y `200` + salida del verificador del bus.

**PT-20 · Expediente externo con ruta CAU-P-122 (movilidad)**

- **Pasos:** igual que PT-19 con `tipoTramite: "EXTERNO"`, `rutaNormativa: "CAU-P-122"`, `institucionOrigen: "Universidad de Cuenca"`.
- **Esperado:** `202 Accepted` con `PENDIENTE_CONTENIDOS`; el evento llega al bus con la ruta `CAU-P-122` y el tipo `EXTERNO`.
- **Variante negativa (PT-20b):** `INTERNO` con `CAU-P-122` y `EXTERNO` con `CAU-P-011` se **aceptan** hoy (el sistema valida dominio, no la combinatoria); se registra como **observación de negocio** para confirmar con el Product Owner en el Sprint 2.
- **Variante negativa (PT-20c):** `tipoTramite: "EXTRANJERO"` ⇒ `400 Bad Request`; `rutaNormativa: "CAU-P-999"` ⇒ `400 Bad Request`.
- **CA:** US-05/CA1; US-06/CA1 · **Evidencia:** capturas `202` de ambos tipos + `400` de las variantes.

**PT-21 · Consulta del estado de recepción**

| Paso | Acción | Esperado |
|---|---|---|
| 1 | `GET /api/expedientes/{id}` de PT-19 | `200 OK` con `estado`, `correlationId`, `eventoPublicado`, `intentosPublicacion`, `ultimoErrorPublicacion`, `fechaCreacionUtc`, `fechaPublicacionUtc`, documentos y asignaturas |
| 2 | `GET /api/expedientes/{guid aleatorio}` | `404 Not Found` |
| 3 | `GET /api/expedientes?estado=PENDIENTE_CONTENIDOS` | `200 OK` con la lista (máx. 100, más recientes primero) |
| 4 | `GET /api/expedientes` con token de DOCENTE | `403 Forbidden` |
| **CA** | US-05/CA3 | **Evidencia:** capturas `200`, `404`, `403` |

**PT-22 · Restricciones de archivos PDF (estado y criterios de aceptación)**

Criterio de aceptación esperado de la carga documental (responsabilidad de S1-S05/S1-S06, Microservicio B):

| # | Regla | Comportamiento esperado |
|---|---|---|
| 1 | Formato | Solo `application/pdf`; se rechaza cualquier otro MIME/extensión **y** cualquier archivo cuya firma no sea `%PDF-` |
| 2 | Tamaño | Límite máximo configurable (propuesto: 10 MB por documento, 50 MB por expediente); se rechaza con `400` |
| 3 | Nombre | Sin rutas ni caracteres de escape (`../`, `\`, `%00`); se sanea y se rechaza si es inválido |
| 4 | Integridad | `HashSha256` calculado por el servidor y verificado contra el declarado; discrepancia ⇒ `400` |
| 5 | Almacenamiento | Fuera del *webroot*, nombre generado por el sistema, sin exposición pública directa |
| 6 | Malware | Análisis antivirus antes de aceptar el documento |
| 7 | Referencia en el evento | El evento transporta **solo** `DocumentoIds`, nunca el binario (R-01 del contrato) |
| 8 | Trazabilidad | Cada documento conserva nombre original, hash, tamaño y fecha de carga en `EXPEDIENTE_DOC` |

**Resultado de la verificación del Sprint 1:** las reglas 1, 2, 3 y 8 **no están implementadas** todavía
(`DocumentoRequest` solo valida longitudes), por lo que PT-22 queda **NO EJECUTABLE** y se registra el
hallazgo **H-04** (severidad Media-Alta, riesgo de integridad documental). La regla 7 sí se verifica hoy
mediante PT-23 y PT-24.

- **Evidencia:** captura del DTO en Swagger (`DocumentoRequest`) mostrando las únicas validaciones existentes + este registro del hallazgo.

### 4.6 US-06 · Contrato del evento y publicación asíncrona (PT-23 a PT-25)

**PT-23 · Contrato `SolicitudCreadaEvent` publicado sin binarios (CA1 y CA2)**

- **Precondición:** RabbitMQ arriba; verificador del bus suscrito (`dotnet run --project tools/Siha.BusVerifier`).
- **Pasos:** 1) registrar un expediente (PT-19); 2) comparar la salida del verificador con
  `docs/contrato-solicitud-creada-event.md`; 3) inspeccionar el mensaje en el panel de RabbitMQ.
- **Esperado:**
  - Los **12 campos** del contrato con los valores del expediente.
  - `CorrelationId` del evento **igual** al devuelto en el `202 Accepted`.
  - `documentoIds` con **referencias** (UUID) y **ningún** contenido binario; tamaño del mensaje < 4 KB.
  - `content_type = application/vnd.masstransit+json`; exchange `Siha.Contracts.Events:SolicitudCreadaEvent` (fanout); `delivery_mode = 2`.
  - El consumidor de referencia del MS B registra el evento en su log.
- **CA:** US-06/CA1, CA2 · **Evidencia:** captura del verificador con el evento completo + captura del panel (cola con 1 consumidor y propiedades del mensaje) + `docker compose logs ms-b | Select-String "SolicitudCreadaEvent"`.

**PT-24 · Fallo y recuperación del bus (S1-P09) — caso crítico**

- **Precondición:** stack completo arriba y al menos un expediente registrado correctamente (PT-19).
- **Pasos:**
  1. Detener el bus: `docker compose stop rabbitmq`.
  2. `POST /api/expedientes` con un expediente nuevo (datos de PT-19, código distinto).
  3. Anotar `id`, `estado`, `publicado`, `correlationId` y `ultimoErrorPublicacion`.
  4. `GET /api/expedientes/{id}` para confirmar la persistencia y el estado recuperable.
  5. `GET /api/expedientes?estado=REGISTRADO_PENDIENTE_PUBLICACION`.
  6. Levantar el bus: `docker compose start rabbitmq` y esperar *healthy*.
  7. `POST /api/expedientes/{id}/republicar`.
  8. Comparar el `correlationId` de los pasos 3 y 7.
- **Esperado:**
  - Paso 2 → **`202 Accepted`** (no `500`): el expediente **se persiste aunque el bus falle** y `publicado = false`.
  - Paso 3/4 → `estado = REGISTRADO_PENDIENTE_PUBLICACION`, `intentosPublicacion ≥ 4` (3 reintentos del publicador + 1), `ultimoErrorPublicacion` con el detalle del fallo, `eventoPublicado = false`.
  - Paso 7 → `202 Accepted` con `publicado = true`, `estado = PENDIENTE_CONTENIDOS` e **`intentosPublicacion` incrementado**.
  - Paso 8 → **el `CorrelationId` es exactamente el mismo** (regla de oro de §4 del contrato). Un cambio de identificador ⇒ **defecto bloqueante**.
  - Tras la republicación, el verificador del bus imprime el evento con ese `CorrelationId`.
- **CA:** US-06/CA3 · **Evidencia:** capturas de los pasos 2, 4, 5 y 7 + salida del verificador tras republicar + `docker compose logs ms-b` con los tres reintentos fallidos.

**PT-25 · Trazabilidad y observabilidad**

| Paso | Acción | Esperado |
|---|---|---|
| 1 | Buscar el `CorrelationId` en `docker compose logs ms-b` | Aparece en cada intento de publicación con `ExpedienteId` e `Intento=n/3` |
| 2 | Buscar el mismo identificador en la salida del verificador | Coincide (trazabilidad extremo a extremo B → bus → consumidor) |
| 3 | `GET /api/expedientes/{id}` | Expone `correlationId`, `intentosPublicacion` y `fechaPublicacionUtc` |
| **CA** | US-06/CA3 | **Evidencia:** capturas de logs y de la respuesta |

---

## 5. Batería de regresión funcional (S1-P10)

Se ejecuta **completa** al cierre del Sprint 1 y cada vez que se corrige un defecto o se integra un
cambio en `Siha.Contracts`, en el Gateway o en el Microservicio B.

| ID | Bloque de regresión | Casos que incluye | Criterio de aprobación |
|---|---|---|---|
| **PT-R01** | Seguridad y RBAC | PT-01 a PT-05, PT-09, PT-16, PT-17 | Todos los códigos `200/201/400/401/403/409` esperados; ningún endpoint protegido accesible sin rol |
| **PT-R02** | Reglas y umbrales | PT-06 a PT-08 | δ ≤ θ respetado; ningún `400` muta la configuración previa |
| **PT-R03** | Catálogo e importación | PT-10 a PT-15 | 43 planes importados; duplicados rechazados; planes incompletos en `planCompleto = false` |
| **PT-R04** | Expedientes | PT-19 a PT-21 | `202` + `PENDIENTE_CONTENIDOS` para INTERNO/EXTERNO; `400` en dominio inválido; `404` en inexistente |
| **PT-R05** | Contrato del evento | PT-23, PT-25 | 12 campos, sin binarios, `CorrelationId` coincidente, `content_type` correcto |
| **PT-R06** | Recuperación del bus | PT-24 | Estado `REGISTRADO_PENDIENTE_PUBLICACION` y republicación con el **mismo** `CorrelationId` |
| **PT-R07** | Pruebas automatizadas | `dotnet test tests/Siha.MsA.Tests/Siha.MsA.Tests.csproj` | **0 fallos** (incluye `RbacTests`: roles por reflexión y validación de θ/δ/τ) |
| **PT-R08** | Compilación de la herramienta de verificación | `dotnet build tools/Siha.BusVerifier/Siha.BusVerifier.csproj` | Compila sin errores ni advertencias |

**Registro de ejecución de la regresión** (se completa en cada corrida):

| Fecha | Ejecutor | Build / commit | PT-R01 | PT-R02 | PT-R03 | PT-R04 | PT-R05 | PT-R06 | PT-R07 | PT-R08 | Observaciones |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 18-oct-2026 | P. Acurio (QA) | Sprint 1 (validación de build y pruebas automatizadas) | Pendiente de corrida funcional con el stack levantado | Pendiente | Pendiente | Pendiente | Parcial (verificador validado con `--prueba`) | Pendiente | **Aprobado** (0 fallos) | **Aprobado** (0 errores) | Ver §8 «Estado de la verificación» |

---

## 6. Criterios de entrada, salida, suspensión y reanudación

| Tipo | Condición |
|---|---|
| **Entrada** | Historias US-01…US-06 «Ready» y desplegadas; `docker compose up -d --build` con todos los servicios *healthy*; datos semilla cargados; contrato del evento aprobado (S1-P01) |
| **Salida** | 100 % de los casos **ejecutables** con evidencia; 0 defectos **Bloqueante/Alta** abiertos; PT-R01…PT-R08 aprobados; hallazgos H-01…H-06 registrados con responsable |
| **Suspensión** | Fallo del entorno (SQL Server o RabbitMQ caídos), contrato del evento cambiado sin actualizar `Siha.Contracts`, o build roto en `main` |
| **Reanudación** | Entorno restablecido y build verde; se reintenta el caso suspendido y, si toca RBAC o contrato, se repite el bloque de regresión afectado |

**Severidad de defectos**

| Severidad | Definición | Ejemplo del Sprint 1 |
|---|---|---|
| **Bloqueante** | Impide completar la historia o rompe el contrato | El `CorrelationId` cambia al republicar; el evento adjunta binarios; `POST /api/expedientes` devuelve `500` con el bus caído |
| **Alta** | Bloquea un flujo completo por el punto de entrada único | H-01 (importación de los 43 planes no publicada en el Gateway: `404`) |
| **Media** | Afecta reglas de negocio o seguridad sin bloquear el flujo | H-04 (sin validación binaria de PDF); H-02 (403 indebido en consultas por el borde) |
| **Baja** | Inconsistencia o mejora | H-03 (`400` en lugar de `409` al duplicar regla) |

---

## 7. Trazabilidad criterios de aceptación ↔ casos

| Historia | Criterio | Casos que lo verifican |
|---|---|---|
| US-01 | CA1 (hash BCrypt, nunca expuesto) | PT-03, PT-05 |
| US-01 | CA2 (JWT con rol; 401 si inactiva/incorrecta) | PT-01, PT-02, PT-05 |
| US-01 | CA3 (solo admin; 403 y 409) | PT-03, PT-04 |
| US-02 | CA1 (regla con tipo, versión, fuente y vigencia) | PT-06 |
| US-02 | CA2 (400 si δ > θ sin modificar configuración previa) | PT-07, PT-08 |
| US-02 | CA3 (solo admin modifica; 403) | PT-09 |
| US-03 | CA1 (consulta de carrera, versión y asignatura) | PT-06, PT-10, PT-11 |
| US-03 | CA2 (API REST del catálogo) | PT-10 |
| US-03 | CA3 (duplicados rechazados; planes incompletos pendientes; 43 planes) | PT-11, PT-12, PT-13, PT-14, PT-15 |
| US-04 | CA1/CA2 (401/403 en el borde) | PT-16, PT-17, PT-18b |
| US-04 | CA3 (404/errores controlados) | PT-18 |
| US-05 | CA1 (registro de expediente interno y externo) | PT-19, PT-20 |
| US-05 | CA3 (consulta del estado de recepción) | PT-21 |
| US-06 | CA1 (evento con referencias, sin binarios) | PT-23 |
| US-06 | CA2 (HTTP 202 y `PENDIENTE_CONTENIDOS`) | PT-19, PT-20, PT-23 |
| US-06 | CA3 (estado recuperable y reintento con correlación) | PT-24, PT-25 |

---

## 8. Estado de la verificación al cierre del Sprint 1

| Verificación | Resultado | Evidencia |
|---|---|---|
| `dotnet build tools/Siha.BusVerifier/Siha.BusVerifier.csproj` | **Aprobado** — 0 errores, 0 advertencias | Salida de consola del build |
| `dotnet test tests/Siha.MsA.Tests/Siha.MsA.Tests.csproj` | **Aprobado** — 53 pruebas superadas, 0 con error (incluye `RbacTests`, nuevo) | Salida de `dotnet test` |
| Arranque y suscripción del verificador del bus (`S1-P04`) | **Aprobado** — conexión al bus, cola `siha-verificador-solicitud-creada` con 1 consumidor | Salida del verificador |
| Autoprueba del contrato (`--prueba`) | **Aprobado** — el `SolicitudCreadaEvent` sintético viaja íntegro (12 campos, `CorrelationId` ida = vuelta) | Salida del verificador |
| Contrato de la publicación en el bus | **Aprobado** — `content_type = application/vnd.masstransit+json`, `delivery_mode = 2`, exchange fanout del contrato, `correlation_id` presente en las propiedades AMQP | Captura del panel RabbitMQ (§5.2 del contrato) |
| Casos PT-01…PT-25 | **Pendiente de corrida funcional** con el stack completo (A + B + Gateway) levantado; requieren el entorno de ejecución y las capturas de Swagger/Postman | Se completan en la ejecución conjunta del equipo |
| PT-15 y PT-22 | **Bloqueado / No ejecutable** por los hallazgos H-01 y H-04 | `matriz-rbac.md` §7 |

---

## 9. Riesgos y dependencias

| # | Riesgo | Probabilidad | Impacto | Mitigación |
|---|---|---|---|---|
| R1 | El Gateway no publica `POST /api/asignaturas/importar` (H-01) | Alta (confirmada por configuración) | Alto: bloquea la carga masiva por el punto de entrada único | Corregir `ocelot.json` (añadir `POST` con `RouteClaimsRequirement` de administrador) y re-ejecutar PT-15 + PT-R03 |
| R2 | Sin validación binaria de PDF (H-04) | Alta | Medio-Alto: integridad documental | Implementar en S1-S05/S1-S06 y activar PT-22 en la regresión del Sprint 2 |
| R3 | Dependencia del Microservicio C (Sprint 2) para cerrar el ciclo | Media | Medio: la verificación termina en el bus | Mantener el verificador y el contrato como contrato congelado y versionado |
| R4 | Entorno de ejecución compartido (una sola instancia de RabbitMQ/SQL) | Media | Medio: interferencias entre pruebas de distintos integrantes | Usar colas y datos con prefijo `QA`; purgar colas antes de cada corrida; ejecutar la regresión en ventana acordada |
| R5 | Cambios de contrato sin actualizar `Siha.Contracts` | Baja | Alto: rompe al consumidor | Regla EV-06 del contrato + PT-R05 en la regresión |

---

## 10. Evidencias requeridas (lista de verificación para el informe)

- [ ] Captura de **Swagger** del Microservicio A (`:5081/swagger`) con los endpoints de autenticación, usuarios, reglas, carreras, mallas y asignaturas.
- [ ] Captura de **Swagger** del Microservicio B (`:5082/swagger`) con `POST /api/expedientes` y `POST /api/expedientes/{id}/republicar`.
- [ ] Capturas de respuestas **`202 Accepted`** de expediente INTERNO (CAU-P-011) y EXTERNO (CAU-P-122).
- [ ] Capturas de **`400`** (δ > θ, valores fuera de rango, tipo/ruta inválidos) y de la **no mutación** de los umbrales.
- [ ] Capturas de **`401`** (sin token) y **`403`** (roles no autorizados) en el borde.
- [ ] Capturas de **`404`** (recurso inexistente) y **`409`** (correo/código/versión duplicados).
- [ ] Captura del resultado de la **importación de los 43 planes** (`importacionResultado`) y de los casos de duplicados, 48 h/crédito y planes incompletos.
- [ ] Captura del **verificador del bus** con el `SolicitudCreadaEvent` completo.
- [ ] Captura del **panel de RabbitMQ**: exchange `Siha.Contracts.Events:SolicitudCreadaEvent`, cola con consumidor y propiedades del mensaje (`content_type`, `correlation_id`).
- [ ] Capturas de la **prueba de fallo y recuperación**: estado `REGISTRADO_PENDIENTE_PUBLICACION`, reintentos en el log y republicación con el **mismo** `CorrelationId`.
- [ ] Salida de `dotnet test` (**0 fallos**) y de `dotnet build` de la herramienta de verificación.
- [ ] Colección Postman ejecutada (captura del *Collection Runner* o salida de **Newman**).

---

## 11. Referencias

- `docs/contrato-solicitud-creada-event.md` — contrato del evento (S1-P01).
- `docs/matriz-rbac.md` — matriz de accesos y validación (S1-P02/S1-P03/S1-P08).
- `docs/postman/SIHA-IA-Sprint1.postman_collection.json` — colección ejecutable.
- `tools/Siha.BusVerifier/README.md` — suscriptor de prueba del bus (S1-P04).
- `tests/Siha.MsA.Tests/RbacTests.cs` — pruebas de RBAC y de validación de umbrales (S1-P07).
- `README.md` del repositorio — puesta en marcha y flujo de extremo a extremo.
