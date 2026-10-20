# Validación funcional, normativa y evidencias · Sprint 1 · SIHA-IA

**Rol:** Paul Acurio — Product Owner Proxy & AI Lead (normativa + QA funcional)
**Fecha de ejecución:** 20 de octubre de 2026
**Entorno:** Windows · Docker Desktop (pila completa del `docker-compose.yml`: SQL Server, RabbitMQ, MsA, MsB, Gateway Ocelot `:5000`, Frontend) · .NET SDK 8 (suites `net8.0`) · Node 20 + Newman 6 · Python 3.13 + `pypdf`
**Fuentes oficiales usadas (sin inventar datos):**

| Fuente | Uso |
|---|---|
| `C:\Users\User\Downloads\Planificacion_SIHA-IA (1).pdf` (66 pág., texto en `Temp\opencode\planificacion_siha_ia.txt`) | Alcance **43 planes analíticos de Software**, RN-01…RN-19, criterios US-01…US-06, tareas S1-P* |
| Carpeta `ddd\ddd\PLAN ANALÍTICO\` (64 PDF reales) | Materias, créditos, horas y unidades temáticas citadas en este informe |
| Código del repositorio | Semilla `DbSeederA`, validadores, `ArchivoStorageService`, ocelot.json |
| `docs/plan-de-pruebas-sprint-1.md` · `docs/matriz-rbac.md` · `docs/contrato-solicitud-creada-event.md` | Casos PT-*, hallazgos H-01…H-06, contrato del evento |

---

## 1. Resumen ejecutivo

| # | Tarea de validación | Resultado | Evidencia |
|---|---|---|---|
| 1 | Conciliación catálogo real vs alcance oficial | **24 de 43 planes de Software** en la carpeta → **brecha de 19 planes**; 64/64 PDF cumplen 48 h/crédito y `Σ unidades = horas`; 0 duplicados por código UTA | `catalogo-planes-analiticos-20261020.md` |
| 2 | Catálogo estructurado | Payload JSON real del importador generado desde los 64 PDF (24 SF + 40 TI) | `../../catalogo/catalogo-estructurado-2026.json` |
| 3 | Importación con datos reales | **MsA 58/58** (5 pruebas nuevas con datos reales) + PT-11…PT-15 verdes | `simuladas-msa-20261020.trx`, `newman-carpetas00-04-20261020.log` |
| 4 | RN oficiales vs reglas iniciales | RN-01…RN-19 extraídos textuales; semilla cubre **5/19** con mapeo documentado + 1 discrepancia semántica (`tau>=65` vs `τ=0,75`) | este informe §5 |
| 5 | Separación normativo/heurístico | US-02/CA1-CA3 **cumplidos**; discrepancias de defaults `θ/δ/τ` frente al modelo oficial documentadas | este informe §6 |
| 6 | Matriz funcional de roles | **Newman 75 requests · 132/132 aserciones · 0 fallos** (401/403/404/409/400 por endpoint y rol) + auditoría de rutas 404/502 | `newman-carpetas00-04-20261020.log/.json`, `gateway-audit-s1p08-20261020.log` |
| 7 | Flujo PDF → expediente → bus | **Fase A (bus caído) 8/8 · 15/15** y **Fase B (bus operativo) 13/13 · 24/24**; recuperación automática verificada | `newman-pt24-faseA-*`, `newman-pt24-faseB-*` |
| 8 | Hallazgos y consolidación | H-01 y H-02 **corregidos**, H-04 **cerrado**; H-03/H-05/H-06 abiertos; estado de tareas §9 | `hallazgos-h01-h06-estado-20261020.log`, `h01-rbac-gateway-20261020.log` |

**Totales del día:** suites `dotnet test` **118/118** (MsA 58 + MsB 60) · Newman **121 peticiones / 171 aserciones / 0 fallos** (75 + 8 + 13) · pila real Docker en todos los casos (la disponibilidad de un PDF **no** se acepta como prueba de carga).

---

## 2. Tarea 1 — Conciliación del catálogo con la carpeta real

**Criterio:** comparar el alcance oficial contra los PDF realmente disponibles, sin inventar materias ni duplicados; los campos no extraíbles se omiten o se marcan para revisión.

**Fuente oficial (planificación §2):** *«43 asignaturas por nivel… Software: 9 niveles, 135 créditos, 6480 horas, julio–diciembre 2026»* — el alcance del Sprint 1 importa **solo Software (SF)**.

**Realidad de la carpeta `ddd\ddd\PLAN ANALÍTICO\` (64 PDF):**

| Cohorte | Planes | Créditos | Horas | Semestres |
|---|---|---|---|---|
| **Software (SF)** | **24** | 70 | 3360 | 1.º–8.º (sin 9.º) |
| Tecnologías de la Información (TI) | 40 | 119 | 5712 | 1.º–9.º |
| Total carpeta | 64 | 189 | 9072 | — |

**Resultado (tabla completa en `catalogo-planes-analiticos-20261020.md`, 147 líneas):**

- **Brecha: faltan 19 planes de Software** (43 − 24) ≈ **65 créditos y 3120 horas**; el **9.º nivel de SF está vacío**. No existe lista maestra de los 43 en los instructivos → **no se inventó**: la brecha queda como insumo pendiente para Coordinación de Carrera/PO.
- La premisa interna «40 PDF de Software + 40 de TI» es **incorrecta**: son **24 + 40**.
- **0 duplicados** por código UTA en las 64 materias; «INVESTIGACION OPERATIVA» repetida por nombre es **legítima** (existe en SF y en TI).
- **64/64** planes cumplen **48 h por crédito** (RN-15) y **Σ unidades temáticas = horas** (RN-14, «no inventar datos») en los dos archivos de control (`horas_x_credito` y `suma_unidades_vs_horas`).
- Las unidades `Prácticas preprofesionales` (60 h) y `Proyecto de titulación` (150 h) no prorratean unidades por asignatura en el PDF → **omitidas del detalle**, no deducidas.

**Estado: ✅ completada con hallazgo (brecha 19).**

---

## 3. Tarea 2 — Coordinación del catálogo estructurado

**Criterio:** generar el payload que consumirá `POST /api/asignaturas/importar`, con solo datos extraíbles del texto de los PDF.

**Resultado:** no existía un catálogo estructurado en el repo; se generó `docs/catalogo/catalogo-estructurado-2026.json` directamente desde los 64 PDF con la **estructura exacta del importador** (`mallas[] → carreras`, `asignaturas[{codigo, nombre, creditos, horas, unidades[{numero, nombre, horas}]}]`):

- **2 mallas**: SF (v1, fuente «ddd/PLAN ANALÍTICO») e TI (v1); `carrera`/`version`/`fuente` viven a nivel de **malla** (`Malla.Fuente`), no por asignatura.
- `contenidos: []` (textos de contenido no extraíbles sin motor IA → **no se inventaron**).
- `unidadesValidadas: true` por plan en `origen` (control `Σ horas = horas del plan`).

**Pendiente de coordinación:** los **19 planes SF faltantes** (Tarea 1) no se pueden cargar hasta que Coordinación/PO los suministre; la importación completa de los 43 queda **bloqueada por insumo**, no por defecto del sistema. La planificación define la dependencia US-03 «catálogo suministrado en formato estructurado» pero **no nombra al responsable de generarlo** → brecha de proceso a resolver en la planificación del Sprint 2.

**Estado: ✅ completada (insumo parcial 24/43, brecha documentada).**

---

## 4. Tarea 3 — Validación de importación con datos reales

**Criterio:** la carga real en la pila debe estar **evidenciada con la pila Docker levantada**, no solo con el archivo disponible; documentar duplicados, rechazos y planes pendientes; no exigir motor IA/OCR (Sprint 2).

**Ejecución:**

1. **Suites xUnit** (`dotnet test`): `Siha.MsA.Tests` **58/58** — incluye las 5 nuevas `CatalogoImportacionRealTests` con datos **reales de los PDF**: malla SF completa (24 asignaturas), «METODOLOGÍAS ÁGILES» SF 4 cr/192 h/9 unidades, «ADMINISTRACIÓN DE BASES DE DATOS» TI 3 cr/144 h, «DESARROLLO DE PROYECTOS» TI 6 cr/288 h/9.º nivel, conservación de código UTA ≤ 30 caracteres, **duplicado en el lote y duplicado histórico rechazados sin abortar** (1 insertado, 2 rechazados con motivo).
2. **Pila real + Gateway:** Newman PT-11…PT-15 verdes dentro de la corrida de 132 aserciones (§7): lote **43 planes sintéticos QA sin rechazos** (alcanzabilidad del contrato), rechazo de duplicados (409/ya existente), 48 h/crédito verificado, plan incompleto rechazado, y **PT-15 = importación por el Gateway con H-01 corregido (200 ADMIN)**.
3. **Documento incompleto:** un plan sin unidades temáticas queda como **pendiente con advertencia** («no tiene unidades temáticas») y **no pasa a análisis semántico** (US-03/CA3) — respetando que el motor IA es Sprint 2.

**Resultado de la importación real (24/43 disponibles):** importados los 24, **0 rechazos**, 19 pendientes por insumo faltante.

**Hallazgo (brecha menor, documentado):** `Asignatura` **no almacena campo `fuente`**; la fuente vive en `Malla.Fuente`. US-03/CA1 pide «carrera, versión y fuente resoluble»: se cumple **a nivel de carrera+malla**, pero **por asignatura queda implícito** → recomendación S2: denormalizar `Fuente` al importar o exponerla por composición en el DTO.

**Estado: ✅ completada** — `simuladas-msa-20261020.trx` (58/58) + PT-11…15 en `newman-carpetas00-04-20261020.log`.

---

## 5. Tarea 4 — Correspondencia RN oficiales vs reglas iniciales

**Criterio:** contrastar la semilla `DbSeederA` con las reglas **textuales** de la planificación; verificar las fuentes normativas citadas (CAU-P-011 y CAU-P-122) contra los PDF normativos reales.

**Catálogo oficial RN-01…RN-19 (§2.2, textual):** R-01 equivalencia ≥ 80 % contenido/profundidad/carga horaria (CAU-P-011 §B) · RN-02 ≥ 80 % RA (CAU-P-122 Art. 20) · RN-03 vigencia 10 años · RN-04 perfil de egreso · RN-05 informe en 5 días · RN-06 autoridad (Consejo Directivo) · RN-07/RN-08 elegibilidad cambio de carrera/universidad · RN-09 expediente de origen (malla vigente, sílabos) · RN-10 documentos de movilidad · RN-11 continuidad académica · RN-12 conversión de calificaciones (Arts. 19 y 21) · RN-13 métrica de créditos (regla del proyecto; advierte que el 80 % de créditos **no** es atribuible a norma) · RN-14 ponderación temática («no inventar datos») · RN-15 48 h/crédito · RN-16 tercera matrícula · RN-17 registro y vigencia de resolución · RN-18 salida y revisión («Se sugiere» + 100 caracteres = criterios del proyecto) · RN-19 no duplicar créditos.

**Semilla (`DbSeederA.cs`, líneas 60–183) = 5 reglas — mapeo verificado:**

| Semilla | Naturaleza | Oficial equivalente | Veredicto |
|---|---|---|---|
| RN-01 solo materias aprobadas de la malla de origen vigente (CAU-P-011) | Normativa | **RN-09** (parcial) + CAU-P-011 §B/§D.1 | ✅ coherente |
| RN-02 `theta>=80` créditos homologables | **Heurística** (`EsNormativa=false`) | **RN-13** (métrica del proyecto; 80 % no atribuible a norma) | ✅ coherente en naturaleza |
| RN-03 documentación con aprobación certificada (CAU-P-122) | Normativa | **RN-10** (documentos de movilidad) | ✅ coherente |
| RN-04 cobertura temática, `tau>=65` | Heurística | Modelo §2.1 / **RN-14** | ⚠ **discrepancia semántica**: el modelo define `τ = 0,75` (75 en escala 0–100), no 65; la expresión parece confundir τ con la banda de clasificación 65 % |
| RN-05 ≥ 80 % RA (CAU-P-122 **Art. 20**) | Normativa | **RN-02** | ✅ paráfrasis y fuente alineadas |

**Verificación de fuentes normativas (PDF reales de la carpeta `ddd`):**

- **Art. 20 de CAU-P-122 verificado**: «…al menos el 80 % de los resultados de aprendizaje previstos en las unidades de aprendizaje correspondientes» — aparece en el instructivo **«Homologación DIRENI-DAC» (CAU-P-122-2026)**. ⚠ Existe **otra copia** en `ddd` (11 pág., Arts. 1–18, sin Art. 20): **usar la versión firmada DIRENI-DAC** como canónica y retirar/etiquetar la otra.
- **CAU-P-011** respalda RN-01/03/05/07/09: solo materias aprobadas, malla vigente, equivalencia ≥ 80 %, 10 años de antigüedad, 1–2 periodos, informe técnico en 5 días.
- Prefijo real de Software en los PDF: **SF**; el repo (semilla y docs) usa **SW** → discrepancia a corregir en S2 (el importador tolera ≤ 30 caracteres, por eso no rompe).

**Cobertura:** **5/19 filas** en la tabla `Reglas`. RN-06…RN-08, RN-11…RN-12 y RN-16…RN-19 pertenecen al procesamiento del expediente, clasificación e informes (Sprints 2–3). **RN-15 (48 h) y RN-14 (no inventar) sí están implementados en `CatalogoService`** (`HorasPorCreditoUta = 48`) **aunque sin fila propia en `Reglas`** → recomendación: crear las filas o documentar la dualidad.

**Recomendación de numeración:** la semilla y el catálogo oficial **no comparten numeración** (p. ej. semilla RN-02 ≈ oficial RN-13); se recomienda **alininear la semilla a los IDs oficiales RN-01…RN-19** o mantener un mapa `Regla.Codigo` ⇄ RN oficial publicado.

**Estado: ✅ completada con hallazgos (mapeo y discrepancias documentados).**

---

## 6. Tarea 5 — Separación de lo normativo y lo heurístico

**Criterio (US-02, textual de la planificación):**

- **CA1:** *«Cada regla se guarda con su tipo de trámite, versión, fuente y vigencia; los umbrales normativos de CAU-P-011 y CAU-P-122 se mantienen separados de los parámetros heurísticos θ, δ, τ.»*
- **CA2:** *«Valores fuera de rango o una banda inválida (δ > θ) se rechazan con 400 Bad Request sin modificar la configuración anterior.»*
- **CA3:** *«Solo Admin modifica reglas; Docente u Operador reciben 403 Forbidden.»*

**Verificación contra el código y la pila real:**

| CA | Evidencia | Resultado |
|---|---|---|
| CA1 | `ReglaEntity` persiste `Codigo, RutaNormativa, TipoTramite, Descripcion, Expresion, Version, Fuente, EsNormativa, Vigencia, Activa`. Newman: «Crear regla normativa (201) · Versión y fuente registradas», «Crear regla heurística», «Listar reglas filtradas CAU-P-122/EXTERNO». `EsNormativa`: RN-01/03/05 = `true` con fuente normativa; RN-02/04 = `false` («Parámetro heurístico del proyecto»). Los umbrales viven en `ParametrosEvaluacion` **por ruta normativa** → CAU-P-011 y CAU-P-122 separados entre sí **y** de las reglas. | ✅ |
| CA2 | `ParametrosValidator`: θ, δ, τ ∈ [0,100] y **δ ≤ θ**. Newman: `δ > θ` → 400, `θ < 0` → 400, `θ > 100` → 400, `τ > 100` → 400, **sin mutación** de la configuración anterior; `RbacTests.cs` lo cubre por reflexión (MsA). | ✅ |
| CA3 | Newman: sin token → 401; OPERADOR/DOCENTE → 403 en `POST/PUT /api/reglas` y `PUT /api/parametros`; ADMIN → 200. | ✅ |

**Discrepancias de defaults vs modelo oficial (`§2.1`: θ = 80 %, δ = 15 %, `τ = 0,75`):**

| Parámetro | Oficial | Semilla CAU-P-011 | Semilla CAU-P-122 | Estado |
|---|---|---|---|---|
| θ | 80 | 80 | 80 | ✅ |
| δ | 15 | **10** | 15 | ⚠ CAU-P-011 difiere |
| τ | 75 (0,75) | **70** | **65** | ⚠ ambas difieren; `τ=65` coincide con la **banda** «Revisión especial 65–80 %», no con τ |

**Impacto:** **bajo** — los umbrales son configurables por Admin y el validador solo exige rangos y `δ ≤ θ`; ninguna prueba ni criterio se rompe. **Acción recomendada:** alinear la semilla a `θ=80 / δ=15 / τ=75` o registrar la decisión de diseño en la planificación (y corregir la expresión `tau>=65` de RN-04, §5).

**Estado: ✅ completada — normativo y heurístico NO mezclados; discrepancias de defaults documentadas.**

---

## 7. Tarea 6 — Matriz funcional de roles (401/403/404/…) — S1-P02 + S1-P08

**Criterio:** una corrida de la colección completa contra el **Gateway** (`:5000`), con captura de tokens por rol y aserciones por endpoint; además, auditoría de rutas del borde.

**Resultado: `newman-carpetas00-04-20261020.log/.json` → 75 peticiones, 0 fallos de petición, 132/132 aserciones.**

| Situación (endpoint/rol) | Código verificado | Cubre |
|---|---|---|
| Sin token en usuarios, roles, parámetros, carreras, mallas, asignaturas, reglas, expedientes | 401 | US-01/CA3, US-04/CA1 |
| Login con contraseña inválida · cuenta inactiva | 401/401 | US-01/CA2 |
| OPERADOR → usuarios, roles, parámetros, carreras, mallas, asignaturas, reglas | 403 | US-02/CA3, US-04/CA2 |
| DOCENTE → roles, parámetros, reglas, expedientes | 403 | US-05/CA3 |
| ADMIN en los mismos endpoints | 200/201 | US-04/CA1 |
| Recursos inexistentes (usuarios, carreras, mallas, asignaturas, expedientes, parámetros de ruta) | 404 | US-04/CA3 |
| Duplicados (usuario, carrera, malla) · regla duplicada | 409 · **400** (H-03, documentado) | US-01/CA1 |
| Rol inexistente, contraseña corta, δ > θ, θ/τ fuera de rango, no-PDF, docs declarados, lote incompleto | 400/422 | US-01/CA3, US-02/CA2, US-05/CA2 |
| **Ruta desconocida vía Gateway** (con y sin token) | **404** | US-04/CA3 |
| **Servicio MsB caído** (parada controlada + recuperación) | **502** (5xx controlado, nunca 200 simulado) → 200 tras `docker start` | US-04/CA3 (503/5xx) |
| **H-01 importar por Gateway** | 401 sin token · 403 DOCENTE · 403 OPERADOR · **200 ADMIN** | US-03/CA3 |
| **H-02** lecturas `GET carreras/{id}` y `GET asignaturas/{id}` con DOCENTE/OPERADOR | **200** vía Gateway = 200 directo (consistente); escritura conserva 403 | matriz §2.4 actualizada |

**Correcciones aplicadas a la colección durante esta validación (3):**

1. Test con `'{{prefijo}}'.toUpperCase()` (script roto en el anidamiento del template) → `pm.collectionVariables.get('prefijo').toUpperCase()`.
2. PT-15 renombrado a «Hallazgo H-01 corregido» y sus tests **404 → 200** (el hallazgo está corregido en el código).
3. Defecto de lectura en mi propia extracción de fases (auth a nivel `request.auth`) — sin efecto en la colección.

**Historia de la corrida:** primera pasada con la colección previa = 132 aserciones / **3 fallos** (1 defecto de script `prefijo` + 2 tests de PT-15 que aún esperaban 404); tras corregir la colección, **corrida final 132/132 · 0 fallos** (el artefacto guardado corresponde a la corrida final verde).

**Estado: ✅ completada** — además de auditoría manual `gateway-audit-s1p08-20261020.log` (404/404/200/**502**/200).

---

## 8. Tarea 7 — Flujo completo PDF → expediente → bus (con caída y recuperación)

**Criterio:** demostrar el flujo **con la pila levantada**, la caída real del bus **sin romper la API**, la recuperación y la conservación del `CorrelationId` (US-05, US-06, S1-P01/P04/P06/P07/P09). No exigir IA/OCR.

### 8.1 Camino nominal (carpeta 04 de la corrida de 132 aserciones)

Alta 201 (borrador `PENDIENTE_DOCUMENTOS`) → rechazo de documentos declarados por el cliente **400** → envío sin documentos **422** → carga de archivo **no PDF** (firma) **400** con motivo → carga de **PDF real** **201** con **SHA-256 calculado por el servidor** → reintento idempotente **200** (mismo expediente) → `POST …/enviar` **202** con `PENDIENTE_CONTENIDOS` y **mismo `CorrelationId`** → segundo envío **200** idempotente → republicar **200** idempotente → consultas 200/404/listado.

### 8.2 Fase A — bus **CAÍDO** (`siha-rabbitmq` detenido): `newman-pt24-faseA-busdetenido-20261020.log/.json` → **8 requests · 15/15 aserciones**

- Alta del borrador **201** (OPERADOR) y carga del PDF **201** (ADMIN) — el flujo previo al envío no depende del bus.
- `POST …/enviar` → **202 Accepted** tras **15,9 s** de reintentos de MassTransit contra el bus caído: **la API no se rompió**, el estado pasa a **`REGISTRADO_PENDIENTE_PUBLICACION`** con `ultimoErrorPublicacion` y **`CorrelationId` conservado** (US-06/CA3).
- `GET /api/expedientes/{id}` → 200 con estado, detalle del fallo y la **misma correlación**; `GET …?estado=REGISTRADO_PENDIENTE_PUBLICACION` → 200 con el expediente listado (recuperable).

### 8.3 Recuperación — evidencia directa

Al re-levantar `siha-rabbitmq` (**healthy**), el expediente de la Fase A **transicionó solo** a `PENDIENTE_CONTENIDOS` con `ultimoErrorPublicacion` vacío y **mismo `correlationId` `47d1e7c3-6360-42e1-abdb-93fe4a78245a`** → reintento automático del mensaje encolado (S1-P09). Cola **`VerificacionSolicitud`**: consumidores = **1** (MsB suscrito), mensajes = **0** (todos consumidos) — gestión de RabbitMQ `siha/siha-rabbit-2026`.

### 8.4 Fase B — bus **OPERATIVO**: `newman-pt24-faseB-buslevantado-20261020.log/.json` → **13 requests · 24/24 aserciones**

- Republicado: **200** (idempotente, no re-publica), `publicado = true`, **mismo `CorrelationId`**, `PENDIENTE_CONTENIDOS` (US-06/CA3 + trazabilidad).
- Regresión completa PT-R01…PT-R05: 401/403 RBAC, δ ≤ θ (200), catálogo (200), alta → PDF → envío (201 → 201 → 202 con `PENDIENTE_CONTENIDOS`), consulta con correlación (200), salud del Gateway (200).

**Nota de arnes:** las fases se ejecutaron como corridas aisladas; se incrustaron los 3 logins (admin/operador/docente) al inicio de cada fase para capturar los tokens *in-run* y se propagó `expedienteRecuperableId`/`correlationIdRecuperable` de la Fase A a la B (las variables de colección no persisten entre corridas de newman).

**Estado: ✅ completada** — caída, recuperación y regresión evidenciadas en pila real.

---

## 9. Tarea 8 — Hallazgos, evidencias consolidadas y estado de tareas

### 9.1 Estado de hallazgos (verificado hoy contra la pila · `hallazgos-h01-h06-estado-20261020.log`)

| Hallazgo | Estado 20-oct-2026 |
|---|---|
| **H-01** importar no publicado en el Gateway | ✅ **Corregido** — 401/403/403/**200** (`h01-rbac-gateway-20261020.log`) |
| **H-02** borde exige ADMIN en lecturas de catálogo | ✅ **Corregido** — 200 con DOCENTE/OPERADOR en Gateway = directo; escritura 403; celdas ⚠ de `matriz-rbac.md` §2.4 actualizadas |
| **H-03** regla duplicada responde 400 (no 409) | ⏳ Abierto (excepción documentada; 400 confirmado hoy) |
| **H-04** validación binaria PDF no implementada | ✅ **Cerrado** — `ArchivoStorageService`: vacío, tamaño máx. 10 MB, extensión, MIME, firma `%PDF`, SHA-256 del servidor; pila real: PT-19d **400** y PT-19e **201** |
| **H-05** sin «listar mis expedientes» para DOCENTE | ⏳ Abierto por diseño (403 confirmado; dictamen S2) |
| **H-06** sin refresh token / revocación | ⏳ Abierto (sin cambios en S1) |

### 9.2 Evidencias generadas (20-oct-2026)

| Archivo | Contenido |
|---|---|
| `newman-carpetas00-04-20261020.log/.json` | 75 requests · 132/132 aserciones |
| `newman-pt24-faseA-busdetenido-20261020.log/.json` | 8 requests · 15/15 aserciones con RabbitMQ caído |
| `newman-pt24-faseB-buslevantado-20261020.log/.json` | 13 requests · 24/24 aserciones con RabbitMQ operativo |
| `h01-rbac-gateway-20261020.log` | 401/403/403/200 en `POST /api/asignaturas/importar` |
| `gateway-audit-s1p08-20261020.log` | Ruta desconocida 404 (con/sin token) · MsB caído **502** · recuperado 200 |
| `hallazgos-h01-h06-estado-20261020.log` | Estado H-01…H-06 contra la pila |
| `simuladas-msa-20261020.trx` | MsA **58/58** (5 con datos reales de PDF) |
| `simuladas-msb-20261020.trx` | MsB **60/60** |
| `catalogo-planes-analiticos-20261020.md` | Conciliación 24 SF vs 43 oficiales (Tarea 1) |
| `../../catalogo/catalogo-estructurado-2026.json` | Payload real del importador (Tarea 2) |
| `../../postman/SIHA-IA-Sprint1.postman_collection.json` | Colección corregida (3 ediciones) |
| `docs/matriz-rbac.md` | Celdas ⚠ y columna de estado actualizadas |

### 9.3 Estado de las 8 tareas asignadas ⇄ tareas oficiales

| Mi tarea | Tarea oficial | Estado |
|---|---|---|
| 1 · Conciliación catálogo | habilita **S1-P05** / S1-J05 | ✅ completada **con hallazgo: faltan 19 planes SF** |
| 2 · Coordinación catálogo estructurado | **US-03** (insumo de S1-P05) | ✅ completada (24/43; insumo pendiente) |
| 3 · Importación datos reales | **S1-P05** | ✅ completada (58/58 + PT-11…15) |
| 4 · RN vs normativa | **US-02** / S1-J03 | ✅ completada (mapeo 5/19 + discrepancias) |
| 5 · Normativo vs heurístico | **S1-P03** | ✅ completada (US-02 CA1-CA3; defaults θ/δ/τ documentados) |
| 6 · Matriz de roles | **S1-P02** + **S1-P08** | ✅ completada (132/132 + auditoría 404/502) |
| 7 · Flujo PDF→expediente→bus | **S1-P01/P04/P06/P07/P09** | ✅ completada (fases A/B + recuperación) |
| 8 · Consolidación de evidencias | **S1-P10** (regresión US-01…US-06) | ✅ completada (este informe) |

---

## 10. Pendientes (no exigidos en Sprint 1)

1. **19 planes analíticos de Software** → solicitar a Coordinación de Carrera/PO antes de cerrar US-03 (insumo, no defecto).
2. **Motor IA / OCR y gRPC de evaluación semántica** → Sprint 2 (US-08); contenidos del catálogo quedaron vacíos a propósito.
3. **Alinear defaults** `θ=80 / δ=15 / τ=75` y expresión `tau>=65` de la semilla RN-04; alinear numeración RN semilla ⇄ RN-01…RN-19 oficiales; prefijo `SW` (repo) ⇄ `SF` (PDF).
4. **Dos copias de CAU-P-122** en `ddd` → dejar solo la versión DIRENI-DAC (con Art. 20) como canónica.
5. RN-06…RN-08, RN-11…RN-12, RN-16…RN-19 → filas `Reglas` en Sprints 2–3; considerar fila para RN-15 (48 h) y RN-14.
6. `fuente` por asignatura (US-03/CA1 a nivel de agregado) → denormalizar o exponer por composición en S2.
7. H-03 (`409`), H-05 (expedientes del docente), H-06 (refresh token) → backlog S2 según `matriz-rbac.md`.

---

## 11. Cómo reproducir

```powershell
# 1) Pila real (incluye RabbitMQ; para la Fase A: docker stop siha-rabbitmq)
#    IMPORTANTE: transporte_uta_db (puerto 1433) conflicta con siha-sqlserver-a;
#    detenerlo antes: docker stop transporte_uta_db
docker compose up -d
docker ps   # 7 contenedores healthy; Gateway en http://localhost:5000/health

# 2) Suites xUnit (19-oct: 53+60 · 20-oct: 58+60)
dotnet test tests\Siha.MsA.Tests\Siha.MsA.Tests.csproj
dotnet test tests\Siha.MsB.Tests\Siha.MsB.Tests.csproj

# 3) Newman — carpetas 00-04 (75 requests · 132/132)
newman run <carpetas-00-04> --env-var "baseUrl=http://localhost:5000" `
  --reporters "cli,json" --reporter-json-export "docs\evidencias\newman-carpetas00-04-20261020.json"

# 4) Newman — Fase A con el bus CAÍDO (8 · 15/15)
docker stop siha-rabbitmq
newman run <fase-a> --env-var "baseUrl=http://localhost:5000" --reporters "cli,json" `
  --reporter-json-export "docs\evidencias\newman-pt24-faseA-busdetenido-20261020.json"

# 5) Newman — Fase B con el bus OPERATIVO (13 · 24/24)
docker start siha-rabbitmq   # esperar 'healthy'
newman run <fase-b> --env-var "baseUrl=http://localhost:5000" --reporters "cli,json" `
  --reporter-json-export "docs\evidencias\newman-pt24-faseB-buslevantado-20261020.json"

# 6) Auditoría de rutas del Gateway (404 / 502 / recuperación)
Temp\opencode\auditar_gateway.ps1
```

Cuentas QA: `admin@uta.edu.ec / Admin123*` · `operador.qa@uta.edu.ec / Operador123*` · `docente.qa@uta.edu.ec / Docente123*`.
Las corridas aisladas de newman requieren incrustar los logins en la colección (o `--env-var tokenAdmin=…`): la auth por defecto es `{{tokenAdmin}}` y varios requests usan `{{tokenOperador}}`/`{{tokenDocente}}` a nivel request.

> **Nota de entorno:** el contenedor ajeno `transporte_uta_db` **comparte el puerto 1433** con
> `siha-sqlserver-a` (`docker-compose.yml:15`) y ambos no pueden correr a la vez. Al cerrar la
> validación se restauró el estado original del equipo: la pila SIHA quedó **detenida** con
> `docker compose stop` (contenedores y volúmenes conservados) y `transporte_uta_db` **activo**.
> Para re-ejecutar esta validación: `docker stop transporte_uta_db` y luego `docker compose up -d`.
