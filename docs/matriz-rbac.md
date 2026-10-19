# Matriz RBAC y de validación · Sprint 1

**Proyecto:** SIHA-IA · **Sprint:** 1 (Base del Sistema) · **Tareas:** S1-P02, S1-P03, S1-P08
**Autor:** Paul Medardo Acurio López — Product Owner Proxy & QA/AI Lead
**Versión:** 1.0 · **Fecha:** 18 de octubre de 2026

Este documento define **qué rol puede invocar cada endpoint**, con el **código HTTP esperado** para
cada combinación, y los **casos de validación** (reglas/umbrales, catálogo, expedientes) que QA
verifica. Es el contrato de acceso del Sprint 1 y la entrada de la prueba de regresión funcional
(S1-P10).

---

## 1. Roles del sistema

Definidos en `src/BuildingBlocks/Siha.Contracts/SihaContratos.cs` (`RolesSistema`), sembrados en DB A y
transportados en el JWT como `ClaimTypes.Role` (`http://schemas.microsoft.com/ws/2008/06/identity/claims/role`).

| Rol | Constante | Propósito | Cuenta de prueba |
|---|---|---|---|
| **ADMINISTRADOR** | `RolesSistema.Administrador` | Gestiona cuentas y roles, define reglas/umbrales y mantiene el catálogo académico | `admin@uta.edu.ec` / `Admin123*` (semilla) |
| **DOCENTE** | `RolesSistema.Docente` | Revisa expedientes y emite dictámenes de homologación (consulta) | crear con `POST /api/usuarios` (rol `DOCENTE`) |
| **OPERADOR** | `RolesSistema.Operador` | Registra expedientes y carga documentos | crear con `POST /api/usuarios` (rol `OPERADOR`) |
| *(sin token)* | — | Anónimo: solo el login | — |

**Dos capas de control de acceso** (defensa en profundidad):

| Capa | Ubicación | Efecto |
|---|---|---|
| **Borde (Gateway Ocelot :5000)** | `Siha.Gateway/ocelot.json` → `AuthenticationOptions` + `RouteClaimsRequirement` | Sin token ⇒ **401**; token sin el rol exigido ⇒ **403**; ruta inexistente ⇒ **404**. |
| **Servicio (Microservicio A/B)** | `[Authorize(Roles = …)]` en controladores y acciones | Vuelve a validar firma, emisor, audiencia y rol: protege el acceso **directo** a :5081 / :5082. |

> Regla de QA: la matriz se cumple **en el borde**. Si un endpoint se invoca directamente al
> microservicio (puerto 5081/5082), la capa de servicio decide; los códigos pueden diferir solo en los
> casos marcados con ⚠ en §7.

**Códigos de la matriz**

| Código | Significado |
|---|---|
| `200` / `201` / `202` / `204` | Operación exitosa (lectura / creación / aceptado asíncrono / sin contenido) |
| `400` | Petición inválida: regla de negocio o validación de parámetros |
| `401` | Sin token, token vencido o firma inválida |
| `403` | Token válido con rol **no autorizado** para el endpoint |
| `404` | Recurso inexistente o ruta no publicada en el Gateway |
| `409` | Conflicto de unicidad (correo, código de carrera, versión de malla, código de asignatura) |

---

## 2. Matriz de accesos por rol y endpoint (API Gateway `http://localhost:5000`)

### 2.1 Autenticación e identidad (US-01)

| Método y ruta | ADMINISTRADOR | DOCENTE | OPERADOR | Sin token |
|---|---|---|---|---|
| `POST /api/auth/login` | `200` | `200` | `200` | `200` (anónimo) · `401` si la credencial es inválida o la cuenta está inactiva |
| `GET /api/auth/yo` | `200` | `200` | `200` | `401` |

### 2.2 Cuentas y roles (US-01) — solo ADMINISTRADOR

| Método y ruta | ADMINISTRADOR | DOCENTE | OPERADOR | Sin token |
|---|---|---|---|---|
| `GET /api/usuarios` | `200` | `403` | `403` | `401` |
| `GET /api/usuarios/{id}` | `200` · `404` si no existe | `403` | `403` | `401` |
| `POST /api/usuarios` | `201` · `400` rol inválido · `409` correo duplicado | `403` | `403` | `401` |
| `PUT /api/usuarios/{id}` | `200` · `400` · `404` | `403` | `403` | `401` |
| `PATCH /api/usuarios/{id}/estado` | `200` · `404` | `403` | `403` | `401` |
| `DELETE /api/usuarios/{id}` | `204` · `404` | `403` | `403` | `401` |
| `GET /api/roles` | `200` | `403` | `403` | `401` |

### 2.3 Reglas y umbrales θ, δ, τ (US-02)

| Método y ruta | ADMINISTRADOR | DOCENTE | OPERADOR | Sin token |
|---|---|---|---|---|
| `GET /api/reglas` (filtros `rutaNormativa`, `tipoTramite`) | `200` | `200` | `200` | `401` |
| `GET /api/reglas/parametros/{rutaNormativa}` | `200` · `404` si la ruta no tiene parámetros | `200` | `200` | `401` |
| `POST /api/reglas` | `201` · `400` ruta inválida o regla duplicada | `403` | `403` | `401` |
| `PUT /api/reglas/parametros/{rutaNormativa}` | `200` · `400` valor fuera de rango o δ > θ · `400` ruta inválida | `403` | `403` | `401` |

### 2.4 Catálogo académico (US-03)

| Método y ruta | ADMINISTRADOR | DOCENTE | OPERADOR | Sin token |
|---|---|---|---|---|
| `GET /api/carreras` | `200` | `200` | `200` | `401` |
| `GET /api/carreras/{id}` | `200` · `404` | `403` ⚠ | `403` ⚠ | `401` |
| `POST /api/carreras` | `201` · `409` código duplicado | `403` | `403` | `401` |
| `PUT /api/carreras/{id}` | `200` · `404` | `403` | `403` | `401` |
| `GET /api/mallas` (`?carreraId=`) | `200` | `200` | `200` | `401` |
| `GET /api/mallas/{id}` | `200` · `404` | `200` | `200` | `401` |
| `POST /api/mallas` | `201` · `400` carrera inexistente · `409` versión duplicada | `403` | `403` | `401` |
| `GET /api/asignaturas` (`?mallaId=`, `?codigo=`) | `200` | `200` | `200` | `401` |
| `GET /api/asignaturas/{id}` | `200` · `404` | `403` ⚠ | `403` ⚠ | `401` |
| `POST /api/asignaturas` | `201` · `400` malla inexistente · `409` código duplicado en la malla | `403` | `403` | `401` |
| `PUT /api/asignaturas/{id}` | `200` · `404` | `403` | `403` | `401` |
| `POST /api/asignaturas/importar` | `200` con `ImportacionResultadoDto` · `400` malla inexistente o lote vacío | `403` | `403` | `401` |

### 2.5 Expedientes y bus (US-05 / US-06)

| Método y ruta | ADMINISTRADOR | DOCENTE | OPERADOR | Sin token |
|---|---|---|---|---|
| `POST /api/expedientes` | `202` · `400` tipo/ruta inválidos | `403` | `202` · `400` | `401` |
| `POST /api/expedientes/{id}/republicar` | `202` · `404` | `403` | `202` · `404` | `401` |
| `GET /api/expedientes/{id}` | `200` · `404` | `403` | `200` · `404` | `401` |
| `GET /api/expedientes` (`?estado=`) | `200` | `403` | `200` | `401` |

**Observación de diseño:** el registro y la publicación del expediente es responsabilidad del
**OPERADOR** (y del ADMINISTRADOR como respaldo); el **DOCENTE** consulta y dictamina en el Sprint 2,
por lo que hoy recibe `403` en el Microservicio B (`[Authorize(Roles = "OPERADOR,ADMINISTRADOR")]`).

---

## 3. Casos de validación de reglas y umbrales (US-02 · S1-P03)

### 3.1 Valores de θ, δ, τ — resultado esperado

| # | θ | δ | τ | Resultado esperado | Mensaje / evidencia |
|---|---|---|---|---|---|
| U-01 | 80 | 10 | 70 | `200 OK` (se crea/actualiza versión) | Parámetros vigentes con `version+1` |
| U-02 | 0 | 0 | 0 | `200 OK` (límite inferior admitido) | — |
| U-03 | 100 | 100 | 100 | `200 OK` (límite superior admitido) | — |
| U-04 | 50 | **80** | 60 | `400 Bad Request` — **δ > θ** | «La banda de tolerancia δ no puede ser mayor que el umbral θ (δ ≤ θ).» |
| U-05 | **-1** | 0 | 0 | `400 Bad Request` — θ negativo | «El umbral θ debe estar en el rango [0, 100].» |
| U-06 | 50 | **-5** | 50 | `400 Bad Request` — δ negativo | «La banda δ debe estar en el rango [0, 100].» |
| U-07 | 50 | 10 | **-0.5** | `400 Bad Request` — τ negativo | «El umbral τ debe estar en el rango [0, 100].» |
| U-08 | **101** | 0 | 0 | `400 Bad Request` — θ > 100 % | Validación de rango (`[Range(0,100)]`) + validador |
| U-09 | 50 | **120** | 50 | `400 Bad Request` — δ > 100 % | Validación de rango + validador (δ ≤ θ) |
| U-10 | 50 | 10 | **100.5** | `400 Bad Request` — τ > 100 % | Validación de rango |
| U-11 | `NaN` / `∞` | — | — | `400 Bad Request` | «Los parámetros θ, δ y τ deben ser valores numéricos finitos.» |
| U-12 | 0 | 0.5 | 0 | `400 Bad Request` — δ > θ con θ = 0 | Igual que U-04 |

**Regla de integridad (US-02 · CA2):** ante cualquier `400`, la configuración anterior **no se
modifica**. Evidencia obligatoria: `GET /api/reglas/parametros/CAU-P-011` antes y después del intento
inválido devuelve la **misma versión** y los mismos valores.

### 3.2 Roles no autorizados y ausencia de token

| # | Rol invocado | Endpoint | Esperado |
|---|---|---|---|
| U-13 | DOCENTE | `PUT /api/reglas/parametros/CAU-P-011` | `403 Forbidden` |
| U-14 | OPERADOR | `PUT /api/reglas/parametros/CAU-P-011` | `403 Forbidden` |
| U-15 | DOCENTE | `POST /api/reglas` | `403 Forbidden` |
| U-16 | OPERADOR | `POST /api/reglas` | `403 Forbidden` |
| U-17 | *(sin token)* | `PUT /api/reglas/parametros/CAU-P-011` | `401 Unauthorized` |
| U-18 | DOCENTE / OPERADOR | `GET /api/reglas` y `GET /api/reglas/parametros/CAU-P-011` | `200 OK` (consulta permitida a todo rol autenticado) |
| U-19 | cualquiera | `GET /api/reglas/parametros/CAU-P-999` | `404 Not Found` (ruta normativa sin parámetros) |
| U-20 | ADMINISTRADOR | `PUT /api/reglas/parametros/CAU-P-999` | `400 Bad Request` (ruta normativa inválida) |

### 3.3 Reglas normativas (US-02)

| # | Caso | Esperado |
|---|---|---|
| R-01 | `POST /api/reglas` con `rutaNormativa = CAU-P-011`, código/versión nuevos | `201 Created` con `fuente`, `version`, `vigenteDesde`, `esNormativa` |
| R-02 | Repetir R-01 (mismo código + ruta + versión) | `400 Bad Request` — «Ya existe una regla con el mismo código, ruta normativa y versión.» |
| R-03 | `POST /api/reglas` con `rutaNormativa = CAU-P-999` | `400 Bad Request` — «La ruta normativa debe ser CAU-P-011 o CAU-P-122.» |
| R-04 | `GET /api/reglas?rutaNormativa=CAU-P-122&tipoTramite=EXTERNO` | `200 OK` con RN-03, RN-04 y RN-05 (semilla) |

---

## 4. Casos de validación del catálogo (US-03 · S1-P02)

### 4.1 Importación masiva de los 43 planes analíticos

| # | Caso | Entrada | Resultado esperado |
|---|---|---|---|
| C-01 | Lote completo (43 planes) con 48 h/crédito y unidades | `POST /api/asignaturas/importar` | `200 OK`; `importadas = 43`, `rechazadas = 0`, `errores = []`; todas con `planCompleto = true` |
| C-02 | **Duplicados**: repetir el mismo código dentro del lote | 2 ítems con `codigo = SW101` | `200 OK`; `importadas = 1`, `rechazadas = 1`; error «Código duplicado en la malla 2024-A: SW101» |
| C-03 | **Duplicado contra la malla existente** (código ya importado antes) | `SW101` de nuevo | `200 OK`; `rechazadas = 1` (misma regla de unicidad por malla) |
| C-04 | **48 h/crédito incumplido**: 4 créditos con 100 h | créditos 4, horas 100 | `200 OK`; ítem importado con `planCompleto = false`; error «no cumple la equivalencia de 48 h/crédito UTA (esperado 192 h, recibido 100 h)» |
| C-05 | **Plan incompleto**: sin unidades temáticas | `unidades: []` | `200 OK`; `planCompleto = false`; error «no tiene unidades temáticas; queda pendiente» |
| C-06 | **Plan incompleto**: créditos no válidos (0) | `creditos = 0` | `200 OK`; `planCompleto = false`; «no declara créditos válidos» |
| C-07 | **Plan inconsistente**: suma de horas de unidades > horas declaradas | 3 unidades × 100 h con 192 h | `200 OK`; `planCompleto = false`; «suma 300 h en unidades y supera las 192 h declaradas» |
| C-08 | **Numeración de unidades repetida** | dos unidades con `numero = 1` | `200 OK`; `planCompleto = false`; «repite el número de unidad» |
| C-09 | Código o nombre vacío | `codigo: ""` | `200 OK`; `rechazadas = 1`; «el código y el nombre son obligatorios» |
| C-10 | Malla inexistente | `mallaId = 999` | `400 Bad Request` — «La malla 999 no existe en el catálogo.» |
| C-11 | Lote vacío | `asignaturas: []` | `400 Bad Request` — «Debe enviar al menos una asignatura para la importación.» |
| C-12 | Rol no autorizado | DOCENTE / OPERADOR | `403 Forbidden` |
| C-13 | Sin token | — | `401 Unauthorized` |

**Regla de negocio verificada:** un plan con `planCompleto = false` **no debe pasar al análisis
semántico** del Microservicio C (queda «pendiente» y debe corregirse antes de usarse en una
homologación).

### 4.2 CRUD de catálogo — conflictos y no encontrados

| # | Caso | Esperado |
|---|---|---|
| C-14 | `POST /api/carreras` con código nuevo | `201 Created` |
| C-15 | Repetir C-14 | `409 Conflict` — «Ya existe una carrera con el código …» |
| C-16 | `POST /api/carreras` con DOCENTE/OPERADOR | `403 Forbidden` |
| C-17 | `GET /api/carreras/999999` | `404 Not Found` |
| C-18 | `POST /api/mallas` con `carreraId` inexistente | `400 Bad Request` — «La carrera 999 no existe.» |
| C-19 | `POST /api/mallas` repitiendo `(carreraId, version)` | `409 Conflict` |
| C-20 | `GET /api/mallas/999999` | `404 Not Found` |
| C-21 | `POST /api/asignaturas` repitiendo código en la misma malla | `409 Conflict` |
| C-22 | `GET /api/asignaturas/999999` | `404 Not Found` |

---

## 5. Casos de validación de expedientes y bus (US-05 / US-06)

| # | Caso | Entrada | Esperado |
|---|---|---|---|
| E-01 | Expediente **INTERNO** con ruta `CAU-P-011` | `tipoTramite=INTERNO`, `rutaNormativa=CAU-P-011` | `202 Accepted`; `estado = PENDIENTE_CONTENIDOS`; `publicado = true`; `correlationId ≠ Guid.Empty` |
| E-02 | Expediente **EXTERNO** con ruta `CAU-P-122` | `tipoTramite=EXTERNO`, `rutaNormativa=CAU-P-122` | `202 Accepted` con `PENDIENTE_CONTENIDOS` |
| E-03 | Tipo de trámite inválido | `tipoTramite = EXTRANJERO` | `400 Bad Request` — «Tipo de trámite inválido. Valores permitidos: INTERNO, EXTERNO.» |
| E-04 | Ruta normativa inválida | `rutaNormativa = CAU-P-999` | `400 Bad Request` — «Ruta normativa inválida. Valores permitidos: CAU-P-011, CAU-P-122.» |
| E-05 | Rol DOCENTE | cualquier `POST /api/expedientes` | `403 Forbidden` |
| E-06 | Sin token | cualquier endpoint de expedientes | `401 Unauthorized` |
| E-07 | Consulta del expediente recién creado | `GET /api/expedientes/{id}` | `200 OK` con `estado`, `correlationId`, `eventoPublicado`, `intentosPublicacion`, documentos y asignaturas |
| E-08 | Expediente inexistente | GUID aleatorio | `404 Not Found` |
| E-09 | Listado por estado | `GET /api/expedientes?estado=PENDIENTE_CONTENIDOS` | `200 OK` (máximo 100, más recientes primero) |
| E-10 | **Fallo del bus**: registrar con RabbitMQ detenido | `docker compose stop rabbitmq` y `POST /api/expedientes` | `202 Accepted`; `estado = REGISTRADO_PENDIENTE_PUBLICACION`; `publicado = false`; `ultimoErrorPublicacion` informado; **sin pérdida del expediente** |
| E-11 | **Republicación**: con el bus levantado | `POST /api/expedientes/{id}/republicar` | `202 Accepted`; `publicado = true`; `estado = PENDIENTE_CONTENIDOS`; **mismo `correlationId`** que E-10 |
| E-12 | Republicar un expediente inexistente | GUID aleatorio + `/republicar` | `404 Not Found` |
| E-13 | Contrato del evento | mensaje en el bus | 12 campos del contrato; `content_type = application/vnd.masstransit+json`; **sin binarios** (solo referencias) |

Detalle de ejecución de E-10/E-11 en `plan-de-pruebas-sprint-1.md` (PT-24).

---

## 6. Requisitos de contraseña y exposición de datos (US-01 · CA1)

| # | Verificación | Esperado |
|---|---|---|
| S-01 | `POST /api/usuarios` con contraseña de menos de 8 caracteres | `400 Bad Request` (validación `MinLength(8)`) |
| S-02 | `POST /api/auth/login` con contraseña incorrecta | `401 Unauthorized` — «Credenciales inválidas o cuenta inactiva.» |
| S-03 | `POST /api/auth/login` con cuenta `activo = false` | `401 Unauthorized` |
| S-04 | Ninguna respuesta de `GET /api/usuarios` expone `passwordHash` | El DTO no contiene el hash (BCrypt solo en DB A) |
| S-05 | `POST /api/usuarios` con el mismo correo (cualquier combinación de mayúsculas) | `409 Conflict` (el correo se normaliza a minúsculas) |
| S-06 | JWT válido pero de otra institución (`iss`/`aud` distintos) | `401 Unauthorized` (validación de emisor y audiencia) |

---

## 7. Hallazgos y observaciones (S1-P02 / S1-P03)

| # | Hallazgo | Impacto | Recomendación |
|---|---|---|---|
| **H-01** | `POST /api/asignaturas/importar` **no está publicado en el Gateway**: la ruta `POST` es `/api/asignaturas` y la ruta `/api/asignaturas/{everything}` solo admite `GET` y `PUT`. Previsible: **`404`** en el borde. | Bloquea la importación de los 43 planes por el punto de entrada único (US-03/CA3) e invalida el ejemplo 7.4 del README. | Añadir `POST` a la ruta `/api/asignaturas/{everything}` (con `RouteClaimsRequirement: ADMINISTRADOR`) o crear una ruta dedicada `/api/asignaturas/importar`. Mientras tanto, la importación se ejecuta contra el Microservicio A (`:5081`) con el mismo JWT; QA lo deja registrado como caso PT-15 y verifica la corrección en la regresión. |
| **H-02** | `GET /api/carreras/{id}` y `GET /api/asignaturas/{id}` exigen ADMINISTRADOR **en el borde** (plantilla `{everything}` con `RouteClaimsRequirement`), pero el controlador solo exige autenticación. | DOCENTE/OPERADOR reciben `403` por el Gateway y `200` si llaman directo al microservicio: matriz inconsistente (marcada ⚠ en §2.4). | Alinear la ruta del borde con el controlador (quitar el claim de rol en la lectura) y reservar el rol para `POST`/`PUT`. |
| **H-03** | Un `POST /api/reglas` duplicado responde **`400`**, no `409`. | Inconsistencia con el resto del catálogo (carreras/mallas/asignaturas usan `409`). | Homologar a `409 Conflict` o documentar la excepción en la matriz (hoy documentada como `400`). |
| **H-04** | La validación binaria de PDF (MIME, tamaño, hash, nombre malicioso) **no está implementada**: `DocumentoRequest` solo valida longitudes. | Riesgo de que un archivo no PDF entre al flujo; el evento transporta referencias a documentos no validados. | Cubrir en S1-S05/S1-S06 (Sebastián) y activar PT-22 como caso de regresión **obligatorio** desde el Sprint 2. |
| **H-05** | No hay endpoint público para «listar mis expedientes» por rol: `GET /api/expedientes` exige OPERADOR/ADMINISTRADOR. | El DOCENTE no puede consultar la cola de trabajo hasta que exista el flujo de dictamen (Sprint 2). | Planificar `GET /api/expedientes/asignados` para el Sprint 2. |
| **H-06** | Sin `refresh token` ni revocación: el token dura `ExpiraMinutos` y una cuenta desactivada conserva acceso hasta que expira. | Ventana de riesgo acotada al tiempo de vida del token. | Evaluar revocación por `jti` en el Sprint 2. |

---

## 8. Automatización de esta matriz

| Mecanismo | Alcance | Cómo se ejecuta |
|---|---|---|
| `tests/Siha.MsA.Tests/RbacTests.cs` (nuevo) | Verifica **por reflexión** que los controladores exigen `ADMINISTRADOR` donde corresponde, que las consultas solo exigen autenticación, que el login es anónimo, que **ningún** endpoint de escritura queda sin ese rol, y que la validación de θ/δ/τ rechaza δ > θ, negativos, > 100 % y valores no finitos | `dotnet test tests/Siha.MsA.Tests/Siha.MsA.Tests.csproj` |
| `docs/postman/SIHA-IA-Sprint1.postman_collection.json` | Ejecuta la matriz completa contra `http://localhost:5000`, con captura automática de tokens y aserciones de estado | Postman / Newman (ver §4 de `plan-de-pruebas-sprint-1.md`) |
| `tools/Siha.BusVerifier` | Verifica el contrato del evento y el `CorrelationId` (E-13, E-11) | `dotnet run --project tools/Siha.BusVerifier` |

Cobertura de la matriz: **todas** las celdas `401`, `403`, `404`, `409` y `400` de §2 a §6 tienen un
caso en la colección de Postman o en las pruebas automatizadas, salvo los hallazgos H-01…H-06, que
quedan registrados como pendientes con su responsable.
