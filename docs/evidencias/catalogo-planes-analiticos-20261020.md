# Conciliación del catálogo: 43 planes analíticos vs. carpeta `ddd`

**Fecha:** 20-oct-2026 · **Responsable:** Paul Medardo Acurio López (Validación funcional, normativa y evidencias) · **Tarea:** S1-P05 / conciliación de alcance (US-03)

## 1. Alcance oficial declarado en la planificación

Fuente: *Informe técnico y planificación por sprints SIHA-IA* (`Planificacion_SIHA-IA.pdf`, 66 pág.), secciones 2.1, 3.1, US-03 CA1 y tareas S1-J05 / S1-P05:

- Sección 2.1: “Mallas Curriculares 2025 y Planes Analíticos: Carrera receptora: **Software (9 niveles, 135 créditos, 6480 horas, 43 asignaturas con planes analíticos completos jul–dic 2026)**. Carreras de origen interno: TI, Industrial, Robótica y Telecomunicaciones.”
- In-Scope (3.1): “catálogo de mallas y **43 planes analíticos de Software** (jul–dic 2026)”.
- US-03 CA1: identificar “carrera, versión y fuente del plan, asignatura, créditos, horas y unidades temáticas **para las 43 asignaturas previstas en el alcance**”.
- Tarea S1-J05: “importación de **43 planes analíticos de Software**, validando créditos, horas por unidad y rechazo de duplicados”.

> El alcance oficial es **solo Software**; los planes de TI son carreras de origen, no del catálogo a importar en la malla receptora.

## 2. Fuente real verificada (carpeta `ddd`, 20-oct-2026)

Carpeta: `C:\Users\User\Downloads\Nueva carpeta\ddd\ddd`

| Ubicación | Contenido | Cantidad |
|---|---|---|
| `PLAN ANALITICO/` | Planes analíticos PDF firmados FISEI (jul–dic 2026) | **64** |
| raíz | Instructivos normativos CAU-P-011-2026 y CAU-P-122-2026 | 4 |
| `Reforma 2026/` | Reforma de instructivo CAU-P-011 (firmada) | 2 |

Clasificación leída de la portada de cada PDF (campo `CARRERA DE:`) y del código UTA (`UTA-FISEI-SF-…` Software, `UTA-FISEI-TI-…` Tecnologías de la Información):

| Carrera | Planes encontrados | Créditos | Horas (semestre) | Semestres cubiertos |
|---|---|---|---|---|
| Software (SF) | **24** | 70 | 3360 | 1.º–8.º (sin 9.º) |
| Tecnologías de la Información (TI) | **40** | 119 | 5712 | 1.º–9.º |
| **Total** | **64** | — | — | — |

## 3. Conciliación y brechas

| Ítem | Alcance oficial | Realidad (carpeta `ddd`) | Estado |
|---|---|---|---|
| Planes de Software | 43 | **24** | ⚠️ **Faltan 19 planes de Software** |
| Niveles de Software | 9 niveles | 8 niveles (1.º–8.º) | ⚠️ Sin planes del 9.º nivel |
| Créditos de Software | 135 | 70 (en los 24 disponibles) | ⚠️ Parcial (52 %) |
| Horas de Software | 6480 | 3360 (en los 24 disponibles) | ⚠️ Parcial (52 %) |
| Premisa “40 PDF de Software y 40 de TI” | — | 24 SF + 40 TI = 64 | ❌ Premisa incorrecta (es 24+40) |
| Duplicados por código UTA | 0 esperados | 0 (64 códigos únicos) | ✅ |
| Duplicados por nombre | — | 1 coincidencia: “INVESTIGACION OPERATIVA” existe en SF **y** en TI con códigos UTA distintos | ✅ Son planes legítimos de carreras distintas, no duplicados |
| Equivalencia 48 h/crédito UTA (RN-15) | Obligatoria | 64/64 cumplen | ✅ |
| Suma de unidades = horas declaradas (RN-14) | Obligatoria | 64/64 cuadran | ✅ |

### 3.1 ¿De dónde salió el “43” y qué falta?

- El **único** respaldo documental del “43” en el repositorio era la documentación QA interna (`docs/plan-de-pruebas-sprint-1.md`, `docs/matriz-rbac.md`, colección Postman y `DbSeederA`); con la planificación oficial recibida el “43” queda **confirmado como alcance aprobado**.
- La carpeta `ddd` **no contiene** los 19 planes faltantes de Software ni una lista maestra (no hay anexos con el listado de asignaturas en los instructivos).
- **No se inventan materias**: los 19 planes faltantes quedan documentados como *brecha de insumo*. La lista oficial completa (43) debe ser suministrada por la Coordinación de Carrera / PO antes de ejecutar la importación completa (S1-P05).

## 4. Listas oficiales extraídas (reproducibles)

Los 64 planes con código UTA, nombre, créditos, horas, semestre y unidades horarias están en:

- `docs/catalogo/catalogo-estructurado-2026.json` — payload exacto de `POST /api/asignaturas/importar` por malla (SF y TI), generado del texto de los PDF sin datos inventados.
- `docs/evidencias/catalogo-planes-analiticos-20261020.md` (este documento).

### Software (SF) — 24 planes disponibles

| # | Código UTA | Asignatura | Cr | H | Sem |
|---|---|---|---|---|---|
| 1 | `UTA-FISEI-SF-MP-UB-01-04` | ALGORITMOS Y LOGICA DE PROGRAMACION | 4 | 192 | 1.º |
| 2 | `UTA-FISEI-SF-MP-UB-02-01` | FUNDAMENTOS DE INGENIERIA DE SOFTWARE | 3 | 144 | 2.º |
| 3 | `UTA-FISEI-SF-MP-UB-03-04` | INTRODUCCION A REDES | 3 | 144 | 3.º |
| 4 | `UTA-FISEI-SF-MP-UB-03-05` | MODELAMIENTO Y DISEñO DE SOFTWARE | 3 | 144 | 3.º |
| 5 | `UTA-FISEI-SF-MP-UP-04-01` | METODOLOGIAS AGILES | 4 | 192 | 4.º |
| 6 | `UTA-FISEI-SF-MP-UP-04-02` | REDES | 3 | 144 | 4.º |
| 7 | `UTA-FISEI-SF-MP-UP-04-03` | BASE DE DATOS | 3 | 144 | 4.º |
| 8 | `UTA-FISEI-SF-MP-UP-04-04` | COMPUTACION VISUAL | 3 | 144 | 4.º |
| 9 | `UTA-FISEI-SF-MP-UP-04-05` | MANEJO Y CONFIGURACION DEL SOFTWARE | 2 | 96 | 4.º |
| 10 | `UTA-FISEI-SF-MP-UP-05-01` | INVESTIGACIÓN OPERATIVA | 3 | 144 | 5.º |
| 11 | `UTA-FISEI-SF-MP-UP-05-03` | PATRONES DE SOFTWARE | 3 | 144 | 5.º |
| 12 | `UTA-FISEI-SF-MP-UP-05-04` | APLICACIONES ORIENTADAS A SERVICIOS | 3 | 144 | 5.º |
| 13 | `UTA-FISEI-SF-MP-UP-05-05` | INTERACCION HUMANO / COMPUTADOR | 3 | 144 | 5.º |
| 14 | `UTA-FISEI-SF-MP-UP-06-02` | APLICACIONES WEB Y MOVILES | 4 | 192 | 6.º |
| 15 | `UTA-FISEI-SF-MP-UP-06-03` | APLICACIONES DISTRIBUIDAS | 3 | 144 | 6.º |
| 16 | `UTA-FISEI-SF-MP-UP-06-04` | GESTION DE PRUEBAS E IMPLANTACIÓN DE SOFTWARE | 3 | 144 | 6.º |
| 17 | `UTA-FISEI-SF-MP-UP-07-02` | DESARROLLO ASISTIDO POR SOFTWARE | 2 | 96 | 7.º |
| 18 | `UTA-FISEI-SF-MP-UP-07-03` | AUDITORIA DE SISTEMAS DE INFORMACION | 3 | 144 | 7.º |
| 19 | `UTA-FISEI-SF-MP-UP-07-04` | GESTION DE PROYECTOS DE SOFTWARE | 3 | 144 | 7.º |
| 20 | `UTA-FISEI-SF-MP-UP-07-05` | GESTION DE CALIDAD DEL SOFTWARE | 3 | 144 | 7.º |
| 21 | `UTA-FISEI-SF-MP-UP-07-06` | INTELIGENCIA ARTIFICIAL | 2 | 96 | 7.º |
| 22 | `UTA-FISEI-SF-MP-UP-08-02` | EMPRENDIMIENTO Y LEGISLACION LABORAL | 2 | 96 | 8.º |
| 23 | `UTA-FISEI-SF-MP-UP-08-03` | SEGURIDAD EN EL DESARROLLO DEL SOFTWARE | 3 | 144 | 8.º |
| 24 | `UTA-FISEI-SF-MP-UP-08-04` | INGENIERIA ECONOMICA PARA SOFTWARE | 2 | 96 | 8.º |

### Tecnologías de la Información (TI) — 40 planes disponibles (carreras de origen)

| # | Código UTA | Asignatura | Cr | H | Sem |
|---|---|---|---|---|---|
| 1 | `UTA-FISEI-TI-MP-UB-01-01` | ALGEBRA LINEAL | 3 | 144 | 1.º |
| 2 | `UTA-FISEI-TI-MP-UB-01-02` | FUNDAMENTOS DE PROGRAMACION | 4 | 192 | 1.º |
| 3 | `UTA-FISEI-TI-MP-UB-01-03` | CALCULO DIFERENCIAL | 3 | 144 | 1.º |
| 4 | `UTA-FISEI-TI-MP-UB-01-04` | FISICA | 3 | 144 | 1.º |
| 5 | `UTA-FISEI-TI-MP-UB-01-05` | METODOLOGIA DE LA INVESTIGACION | 2 | 96 | 1.º |
| 6 | `UTA-FISEI-TI-MP-UB-02-01` | CALCULO INTEGRAL | 2 | 96 | 2.º |
| 7 | `UTA-FISEI-TI-MP-UB-02-02` | LOGICA MATEMATICA | 2 | 96 | 2.º |
| 8 | `UTA-FISEI-TI-MP-UB-02-03` | MEDIDAS ELECTRICAS | 3 | 144 | 2.º |
| 9 | `UTA-FISEI-TI-MP-UB-02-04` | ESTRUCTURA DE DATOS | 3 | 144 | 2.º |
| 10 | `UTA-FISEI-TI-MP-UB-02-05` | REALIDAD NACIONAL | 2 | 96 | 2.º |
| 11 | `UTA-FISEI-TI-MP-UB-02-06` | PROGRAMACION ORIENTADA A OBJETOS | 3 | 144 | 2.º |
| 12 | `UTA-FISEI-TI-MP-UB-03-01` | PROGRAMACION AVANZADA | 3 | 144 | 3.º |
| 13 | `UTA-FISEI-TI-MP-UB-03-02` | PROBABILIDAD Y ESTADISTICA | 3 | 144 | 3.º |
| 14 | `UTA-FISEI-TI-MP-UB-03-03` | SISTEMAS OPERATIVOS | 3 | 144 | 3.º |
| 15 | `UTA-FISEI-TI-MP-UB-03-04` | FUNDAMENTOS DE BASES DE DATOS | 3 | 144 | 3.º |
| 16 | `UTA-FISEI-TI-MP-UB-03-05` | FUNDAMENTOS DE REDES Y COMUNICACION DE DATOS | 3 | 144 | 3.º |
| 17 | `UTA-FISEI-TI-MP-UP-04-01` | METODOS NUMERICOS | 3 | 144 | 4.º |
| 18 | `UTA-FISEI-TI-MP-UP-04-02` | INGENIERIA DE SOFTWARE | 3 | 144 | 4.º |
| 19 | `UTA-FISEI-TI-MP-UP-04-03` | INTERACCION HOMBRE MAQUINA | 3 | 144 | 4.º |
| 20 | `UTA-FISEI-TI-MP-UP-04-04` | ADMINISTRACION DE SISTEMAS OPERATIVOS | 3 | 144 | 4.º |
| 21 | `UTA-FISEI-TI-MP-UP-04-05` | GESTION DE BASES DE DATOS | 3 | 144 | 4.º |
| 22 | `UTA-FISEI-TI-MP-UP-05-01` | SISTEMAS DE BASES DE DATOS DISTRIBUIDOS | 3 | 144 | 5.º |
| 23 | `UTA-FISEI-TI-MP-UP-05-02` | TECNOLOGIAS Y DESARROLLO WEB | 3 | 144 | 5.º |
| 24 | `UTA-FISEI-TI-MP-UP-05-03` | INVESTIGACION OPERATIVA | 3 | 144 | 5.º |
| 25 | `UTA-FISEI-TI-MP-UP-05-04` | CONMUTACION Y ENRUTAMIENTO BASICO | 3 | 144 | 5.º |
| 26 | `UTA-FISEI-TI-MP-UP-05-05` | GESTION Y EVALUACION DE PROYECTOS TI | 3 | 144 | 5.º |
| 27 | `UTA-FISEI-TI-MP-UP-06-01` | ADMINISTRACION DE BASE DE DATOS | 3 | 144 | 6.º |
| 28 | `UTA-FISEI-TI-MP-UP-06-02` | SISTEMAS DE SOPORTE DE DECISIONES | 3 | 144 | 6.º |
| 29 | `UTA-FISEI-TI-MP-UP-06-03` | CONMUTACION Y ENRUTAMIENTO AVANZADO | 3 | 144 | 6.º |
| 30 | `UTA-FISEI-TI-MP-UP-06-04` | GOBIERNOS TI | 3 | 144 | 6.º |
| 31 | `UTA-FISEI-TI-MP-UP-06-05` | APLICACIONES MOVILES | 3 | 144 | 6.º |
| 32 | `UTA-FISEI-TI-MP-UP-07-02` | INTELIGENCIA DE NEGOCIOS | 3 | 144 | 7.º |
| 33 | `UTA-FISEI-TI-MP-UP-07-03` | EMPRENDIMIENTO Y GESTION FINANCIERA DE TI | 4 | 192 | 7.º |
| 34 | `UTA-FISEI-TI-MP-UP-07-04` | ARQUITECTURA Y PLATAFORMAS DE SERVIDORES | 3 | 144 | 7.º |
| 35 | `UTA-FISEI-TI-MP-UP-07-06` | ADMINISTRACION DE REDES | 3 | 144 | 7.º |
| 36 | `UTA-FISEI-TI-MP-UP-08-02` | INTEGRACION DE SISTEMAS | 2 | 96 | 8.º |
| 37 | `UTA-FISEI-TI-MP-UP-08-03` | SEGURIDAD DE LA INFORMACION EN REDES DE COMUNICACION DE DATOS | 3 | 144 | 8.º |
| 38 | `UTA-FISEI-TI-MP-UP-08-04` | AUDITORIA DE TI | 2 | 96 | 8.º |
| 39 | `UTA-FISEI-TI-MP-UP-08-05` | DISEÑO DE PROYECTOS | 3 | 144 | 8.º |
| 40 | `UTA-FISEI-TI-MP-UP-09-02` | DESARROLLO DE PROYECTOS | 6 | 288 | 9.º |

## 5. Reproducción

```powershell
# Requiere: Python 3.13 + pypdf (python -m pip install pypdf)
# 1) Extrae inventario de los 64 PDF y valida 48h/crédito y suma de unidades
python extraer_planes.py      # genera inventario-planes-extractado.json
python generar_catalogo.py    # genera docs/catalogo/catalogo-estructurado-2026.json
# 2) Prueba de importación real (5 casos) incluida en la suite:
dotnet test tests\Siha.MsA.Tests\Siha.MsA.Tests.csproj   # 58/58 (incluye CatalogoImportacionRealTests)
```

> Los scripts `extraer_planes.py` y `generar_catalogo.py` se conservan en la carpeta temporal de ejecución del validador; los resultados quedan embebidos en `catalogo-estructurado-2026.json` (inventario completo) para que esta validación sea reproducible sin re-ejecutar la extracción.
