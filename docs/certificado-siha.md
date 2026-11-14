# Solicitud con certificado SIHA (formato SIHA-CH-01)

Flujo completo, en pocas pantallas:

1. **Operador**: sube un solo PDF, el certificado de homologación preaprobado por Secretaría. En una
   **movilidad externa** puede adjuntar además el sílabo (plan analítico) de cada materia aprobada en la
   otra universidad.
2. **Agente (MS C)**: en cuanto llega, trae de MS A por gRPC las mallas y sílabos oficiales de la carrera
   de origen y de destino y compara cada materia que se pide homologar en tres criterios (mínimo 80 % cada
   uno): **contenidos**, **profundidad** y **carga horaria**. Si los tres llegan, la materia *cumple*; si no,
   queda **a criterio del docente**. Todo se explica unidad por unidad.
   Si el agente no tiene el sílabo de alguna de las dos materias (movilidad externa sin sílabo adjunto,
   materia que no está en la malla o que no tiene unidades publicadas) se muestra **"Sin sílabo para
   comparar"**, sin barras ni porcentajes, y deciden los docentes con el plan analítico del estudiante;
   no cuenta como que falló el 80 %.
3. **Los dos docentes revisores** que vienen en el PDF ven la solicitud con el análisis ya hecho y deciden
   cada materia (Sí, homologar / No homologar). Queda **homologada** si los dos la aprueban.

## Movilidad externa: sílabos de la otra universidad

En un trámite de **movilidad externa** (ruta CAU-P-122) el agente no conoce los sílabos de la institución
de origen. Por eso, en la pantalla de registro, después de leer el certificado aparece la sección
**"Sílabos de la otra universidad (opcional)"** con una fila por cada materia aprobada que el certificado
pide homologar:

- El operador puede **adjuntar un PDF por materia** (el sílabo o plan analítico que entrega el estudiante).
  Al elegirlo se vuelve a validar y se muestra cuántas unidades y horas se leyeron.
- El lector (`Siha.Certificados/LectorSilaboExterno.cs`) es tolerante con el formato: busca unidades como
  `Unidad 1: Nombre (24 horas)` (también Capítulo, Módulo, Tema o Bloque, con números arábigos o romanos)
  y toma como temas las líneas que siguen a cada unidad. Si las unidades no traen horas, las reparte desde
  las `Horas totales` / `Carga horaria` declaradas. También lee `Asignatura:`/`Materia:` y `Créditos:`.
- Un sílabo **se rechaza con un mensaje claro** (y no se registra nada) cuando: no es un PDF con texto,
  no tiene unidades, no indica horas por unidad ni totales, alguna unidad no tiene temas, es en realidad
  un certificado SIHA, corresponde a una materia que el certificado no pide homologar, o se adjuntan dos
  para la misma materia. Los sílabos solo se aceptan en movilidad externa.
- **Adjuntarlos es opcional**: una materia sin sílabo mantiene el comportamiento de siempre
  ("Sin sílabo para comparar", deciden los docentes, no cuenta como que falló el 80 %).

Con el sílabo adjunto, el agente lo usa como malla de origen de esa materia y la compara con el **mismo
método** del trámite interno (embeddings por unidad, cobertura, profundidad y carga horaria), explicando
que las unidades y horas "se tomaron del sílabo de *(institución)* adjuntado al registrar la solicitud".

Los sílabos viajan como documentos del expediente (columna `Tipo` = `SILABO_EXTERNO` y `MateriaOrigen` en
`EXPEDIENTE_DOC`, DB B), llegan a MS C en el evento (`DocumentoReferencia.Tipo`/`MateriaOrigen`, contrato
v1.2) y quedan leídos en `ANALISIS_EXPEDIENTE.SilabosExternosJson` (DB C).

PDFs de ejemplo en `documentos-prueba/silabos-externos` (acompañan a
`certificados-pruebas/V06-movilidad-externa-TI-SW.pdf`): cuatro sílabos válidos de la ESPOCH
(Fundamentos de Programación, Álgebra Lineal, Física y Estructura de Datos) y dos inválidos
(`silabo-sin-horas.pdf`, `silabo-sin-unidades.pdf`). Para regenerarlos:
`python tools/certificados-siha/generar_silabos.py`.

## Conocimiento del agente: sílabos oficiales

`src/Services/Siha.MsA.Api/Data/CatalogoUta/*.json` contiene las mallas de Software, Tecnologías de la
Información, Ingeniería Industrial, Telecomunicaciones y Automatización y Robótica, extraídas de los sílabos
publicados en uta.edu.ec (nivel, créditos, horas y cada unidad con sus temas y horas). MS A las carga al
arrancar como malla vigente `UTA-2022`; si esa malla ya existe en la base, solo le agrega las materias que
falten (por código), sin tocar las que ya están. Para actualizarlas:
`python tools/catalogo-uta/bajar.py SW software` y `python tools/catalogo-uta/parsear.py silabos-SW SW.json`.

## Qué debe traer el PDF (sin créditos, horas ni contenidos)

| Sección | Etiquetas |
|---|---|
| Encabezado | `CERTIFICADO DE HOMOLOGACIÓN` y el código `SIHA-CH-01` |
| Certificado | `Número de certificado`, `Fecha de emisión`, `Emitido por` |
| Estudiante | `Estudiante`, `Cédula` (10 dígitos) |
| Trámite | `Tipo de trámite` (Cambio de carrera / Movilidad externa), `Ruta normativa` (CAU-P-011 / CAU-P-122), `Institución de origen`, `Carrera de origen` y `Carrera de destino` con el **nombre completo** (sin siglas) |
| Revisores | `Docente revisor 1` y `Docente revisor 2` como `Nombre - correo@uta.edu.ec` |
| Historial | Por cada materia cursada: `Materia: nombre completo` y `Nivel`, `Período académico` (ej. `Abr-Sep 2025`), `Nota`, `Estado` (Aprobada / Reprobada) |
| Solicitudes | Por cada pedido: una o varias `Materia aprobada: ...` y `Homologar por: materia de destino` |
| Cierre | `FIN DEL CERTIFICADO` |

Reglas que se verifican al leerlo (instructivo UTA): haber aprobado todo el **primer período académico**,
pedir **al menos una** homologación y **solo con materias aprobadas** (una materia aprobada sirve para una
sola solicitud), materias vigentes (**10 años**), nota coherente con el estado (los mensajes muestran la nota como
`6,50`), dos revisores distintos con
correo institucional que existan en el sistema como **DOCENTE activos**, y carreras que existan en el
catálogo. Reprobar materias después del primer período no impide el cambio.

## Revisión del PDF antes de registrarlo

Al elegir el PDF, la pantalla lo valida (`POST .../certificado/validar`). Si tiene problemas, **no se puede
registrar**: el botón "Registrar y enviar solicitud" queda deshabilitado y se explica qué corregir en tres
tipos, en lenguaje sencillo:

| Tipo | Cuándo | Ejemplos |
|---|---|---|
| **Este PDF no es el certificado SIHA** | Otra plantilla, PDF escaneado o imagen, archivo que no es PDF | E18, E19, `plan-analitico-no-valido.pdf` |
| **El certificado tiene campos sin llenar** | Falta la etiqueta, la etiqueta está pero vacía, o quedó el **texto de ejemplo** de la plantilla (`____`, `XXXX`, `[Nombre completo]`, `AAAA-MM-DD`, `correo@...`, "Escriba aquí"…) | "Falta la cédula del estudiante", "Falta el período académico de «Física»" (E20, E21) |
| **Datos que no cumplen las reglas** | Nota incoherente con el estado, siglas en la carrera, revisor que no existe como docente, materia reprobada pedida, etc. | E01–E17 |

Salvo cuando el archivo no es el certificado, se muestra una **lista de verificación ✔/✘ por sección**
(Certificado, Estudiante, Trámite, Revisores, Historial y Solicitudes) con el motivo de cada problema.
El 422 trae `mensaje`, `tipo` (`NO_ES_CERTIFICADO`, `CAMPO_VACIO`, `REGLA` o `CAMPOS_Y_REGLAS`),
`detalles` (lista simple, como antes), `problemas` (cada uno con su sección y tipo) y `secciones`.

## Tabla RESULTADO_HOMOLOGACION (DB C)

Además del análisis completo guardado como JSON, el agente deja **una fila por cada materia pedida** en la
tabla `RESULTADO_HOMOLOGACION`, para consultarla y reportarla con SQL:

- Expediente, certificado, estudiante, tipo de trámite, ruta, institución y carreras.
- Materia(s) de origen → materia de destino.
- Créditos de origen y de destino y **% de créditos** (informativo), horas de origen y de destino.
- **% contenidos, % profundidad y % carga horaria**, el mínimo exigido y si no hubo sílabo para comparar.
- Estimación del agente (`CUMPLE` / `CRITERIO_DOCENTE`), aprobaciones y rechazos de los docentes y el
  **resultado final** (`PENDIENTE`, `HOMOLOGADA`, `NO_HOMOLOGADA`, `SIN_ACUERDO`).

Se llena cuando el agente termina y se actualiza con cada respuesta de los docentes (sin duplicar filas).
Consulta por API: `GET /api/extracciones/resultados-homologacion?tipoTramite=EXTERNO&resultado=HOMOLOGADA`
(roles operador y administrador). Ejemplo en SQL:

```sql
SELECT MateriasOrigen, MateriaDestino, PorcentajeContenidos, PorcentajeCreditos, EstimacionAgente, ResultadoFinal
FROM RESULTADO_HOMOLOGACION WHERE TipoTramite = 'EXTERNO' ORDER BY AnalizadoUtc DESC;
```

## Contenido mínimo (80 %)

El administrador ve el contenido mínimo en **Reglas de homologación**, pero **no puede bajarlo del 80 %**
que fija el reglamento de la UTA (puede exigir más). Ese único valor se aplica a los tres criterios del
agente: contenidos, profundidad y carga horaria.

## API

| Servicio | Endpoint | Uso |
|---|---|---|
| MS B | `POST /api/expedientes/certificado/validar` | Vista previa de lo leído |
| MS B | `POST /api/expedientes/certificado` | Registra, guarda el PDF y publica `SolicitudCreadaEvent` |
| MS B | `GET /api/expedientes` | El docente solo recibe las solicitudes donde es revisor |
| MS C | `GET /api/extracciones/expedientes/{id}/analisis` | Historial, reglas, análisis explicado, revisores y resultado final |
| MS C | `POST /api/extracciones/expedientes/{id}/analisis-automatico` | Reintentar si la IA no estaba disponible |
| MS C | `POST /api/extracciones/expedientes/{id}/dictamen` | Decisión del docente revisor (rol DOCENTE) |
| MS C | `GET /api/extracciones/resultados-homologacion` | Tabla `RESULTADO_HOMOLOGACION`: una fila por materia (operador y administrador) |
| MS A (gRPC) | `CatalogoGrpc.ObtenerMallaVigente` / `ResolverCarrera` / `VerificarDocentes` | Sílabos para el agente / carreras por nombre / revisores válidos |

## Ejemplos (`documentos-prueba/certificados`, también descargables en la pantalla de registro)

Los revisores de ejemplo (`hnaranjo`, `mvaca`, `jcastro` @uta.edu.ec) se crean como docentes con
`Seed__DocentePassword` (en Docker: `SIHA_DOCENTE_PASSWORD`, o la del administrador).

| PDF | Resultado con los sílabos reales |
|---|---|
| `SIHA-TI-SW-cambio-de-carrera.pdf` | TI → Software. Reprobó 2 materias del segundo nivel. Cumplen: Algoritmos y Lógica de Programación, Física, Álgebra Lineal y Estructura de Datos. Programación Orientada a Objetos queda a criterio del docente (144 h frente a 192 h) |
| `SIHA-IND-SW-cambio-de-carrera.pdf` | Industrial → Software: las cuatro materias quedan a criterio del docente (temas o horas por debajo del 80 %) |
| `SIHA-con-errores.pdf` | Rechazado: siglas en la carrera, primer nivel no aprobado, pide una reprobada, materia de hace más de 10 años, cédula y revisor con Gmail |
| `plan-analitico-no-valido.pdf` | Rechazado: no es la plantilla SIHA-CH-01 |

Para regenerarlos: `pip install reportlab` y `python tools/certificados-siha/generar_certificados.py`.

Las solicitudes registradas con el formato anterior del certificado (sin período académico) ya no aparecen en
la lista de Solicitudes: no se pueden analizar con los sílabos.
