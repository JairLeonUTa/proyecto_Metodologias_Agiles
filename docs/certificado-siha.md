# Solicitud con certificado SIHA (formato SIHA-CH-01)

Flujo completo, en pocas pantallas:

1. **Operador**: sube un solo PDF, el certificado de homologación preaprobado por Secretaría. En una
   **movilidad externa** puede adjuntar además el sílabo (plan analítico) de cada materia aprobada en la
   otra universidad. Si no tiene el PDF, usa **Registro manual** (ver más abajo).
2. **Agente (MS C)**: en cuanto llega, trae de MS A por gRPC las mallas y sílabos oficiales de la carrera
   de origen y de destino y compara cada materia que se pide homologar con la **regla RN-01 (versión 2.1)**:
   al menos **80 % en contenidos y 80 % en créditos**. Todo se explica unidad por unidad. **El agente solo
   recomienda; la decisión de cada materia es siempre de los dos docentes revisores.**
   - Si las dos llegan → **Recomienda homologar**.
   - Si cualquiera no llega → **Recomienda no homologar** y explica qué faltó (por ejemplo, Física con 2
     créditos frente a 3 = 66,67 %). Los docentes igual la deciden: si el estudiante sí vio el tema pero en su
     sílabo se llama distinto, el agente puede no reconocerlo, y los docentes pueden homologarla.
   - Si el agente no tiene el sílabo de alguna de las dos materias (movilidad externa sin sílabo adjunto,
     materia que no está en la malla o que no tiene unidades publicadas) se muestra **"Sin sílabo para
     comparar"**, sin barras ni porcentajes, y deciden los docentes con el plan analítico del estudiante.

   **Créditos**: créditos aprobados (suma si son varias materias) frente a los de la materia de destino, con
   máximo 100 %. Si no se conocen (por ejemplo, un sílabo externo que no los indica) se comparan las horas,
   que es equivalente, y la explicación lo dice. **Profundidad** (horas dedicadas a cada tema) y **carga
   horaria** se muestran solo como datos informativos: no deciden el resultado.
3. **Los dos docentes revisores** que vienen en el PDF ven la solicitud con el análisis ya hecho y deciden
   **todas** las materias (Sí, homologar / No homologar), también las que no llegan al 80 %. Queda
   **homologada** si los dos la aprueban y **no homologada** si los dos la rechazan.
4. **Sin acuerdo** (un docente aprueba y el otro no): la materia la decide el **administrador** (coordinador o
   director de carrera) en la misma pantalla, con un **motivo obligatorio** (tabla `DECISION_FINAL`, DB C). La
   lista muestra "Docentes sin acuerdo · decide el administrador" hasta que lo haga. Los votos de los docentes
   se conservan; si un docente cambia su voto y se ponen de acuerdo, vale la votación.

Si una solicitud se queda en "Estamos recibiendo el certificado…", el agente (MS C) no la recibió (por ejemplo,
estaba apagado al registrarla). La pantalla lo explica a los ~20 s; con el agente encendido
(`docker compose --profile sprint23 up -d`), el operador o el administrador pulsa **«Volver a enviar al
agente»** (`POST /api/expedientes/{id}/reenviar-agente`), que reenvía el mismo evento sin duplicar nada.

## Registro manual (sin PDF)

Menú **Solicitudes → Registro manual** (operador y administrador). El operador escribe los mismos datos del
certificado: estudiante y cédula, tipo de trámite, carreras (de la lista del catálogo), los dos docentes
revisores (de la lista de docentes activos), el historial (materia, nivel, período, nota y estado; la nota
marca sola Aprobada o Reprobada) y las materias que pide homologar (con sugerencias de la malla de destino).

- **Revisar los datos** los valida con exactamente las mismas reglas que un PDF: MS B arma el texto del
  certificado SIHA-CH-01 con lo digitado y lo pasa por `LectorCertificadoSiha`. Si falta algo se muestra la
  misma lista de verificación ✔/✘ por sección. El botón **Registrar** solo se habilita después de una
  revisión sin problemas y si no se cambió nada desde entonces.
- Al registrar, el número es `MANUAL-AAAAMMDD-XXXXXX`, "Emitido por" dice *Registro manual (operador)* y el
  documento del expediente son los datos validados en JSON. MS C lo lee igual que un certificado, así que el
  análisis del agente y la decisión de los docentes son idénticos.

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
método** del trámite interno (embeddings por unidad, contenidos y créditos), explicando
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
- Créditos de origen y de destino, horas de origen y de destino.
- **% contenidos y % créditos** (los que deciden), % profundidad y % carga horaria (informativos), el
  mínimo exigido y si no hubo sílabo para comparar.
- Estimación del agente (`CUMPLE` / `NO_CUMPLE` / `CRITERIO_DOCENTE` sin sílabo), aprobaciones y rechazos de
  los docentes y el **resultado final** (`PENDIENTE`, `HOMOLOGADA`, `NO_HOMOLOGADA`, `SIN_ACUERDO`). Una
  materia `NO_CUMPLE` queda `NO_HOMOLOGADA` desde el análisis, con 0 votos.

Se llena cuando el agente termina y se actualiza con cada respuesta de los docentes (sin duplicar filas).
Consulta por API: `GET /api/extracciones/resultados-homologacion?tipoTramite=EXTERNO&resultado=HOMOLOGADA`
(roles operador y administrador). Ejemplo en SQL:

```sql
SELECT MateriasOrigen, MateriaDestino, PorcentajeContenidos, PorcentajeCreditos, EstimacionAgente, ResultadoFinal
FROM RESULTADO_HOMOLOGACION WHERE TipoTramite = 'EXTERNO' ORDER BY AnalizadoUtc DESC;
```

## Contenido mínimo (80 %)

El administrador ve el contenido mínimo en **Configurar mínimo (80 %)**, pero **no puede bajarlo del 80 %**
que fija el reglamento de la UTA (puede exigir más). Ese único valor se aplica a los dos criterios que
deciden: contenidos y créditos.

Las reglas del catálogo **RN-01** y **RN-13** se versionan solas a la **2.0** al arrancar MS A (también en
bases que ya existían): RN-01 exige `contenido>=80 && creditos>=80` y RN-13 exige `Pcred>=80`. La versión
1.0 (contenido, profundidad y carga horaria; créditos solo como apoyo) queda en el historial, cerrada.
Desde el 24-11-2026 **RN-01 2.1**: el 80 % es la recomendación del agente y la decisión es de los docentes.

## API

| Servicio | Endpoint | Uso |
|---|---|---|
| MS B | `POST /api/expedientes/certificado/validar` | Vista previa de lo leído |
| MS B | `POST /api/expedientes/certificado` | Registra, guarda el PDF y publica `SolicitudCreadaEvent` |
| MS B | `GET /api/expedientes/manual/opciones` | Docentes revisores y materias de las carreras para el registro manual |
| MS B | `POST /api/expedientes/manual/validar` · `POST /api/expedientes/manual` | Revisar / registrar una solicitud digitada a mano |
| MS B | `GET /api/expedientes` | El docente solo recibe las solicitudes donde es revisor |
| MS C | `GET /api/extracciones/expedientes/{id}/analisis` | Historial, reglas, análisis explicado, revisores y resultado final |
| MS C | `POST /api/extracciones/expedientes/{id}/analisis-automatico` | Reintentar si la IA no estaba disponible |
| MS C | `POST /api/extracciones/expedientes/{id}/dictamen` | Decisión del docente revisor (rol DOCENTE) |
| MS C | `POST /api/extracciones/expedientes/{id}/decision-final` | Decisión final del administrador en una materia sin acuerdo (motivo obligatorio) |
| MS B | `POST /api/expedientes/{id}/reenviar-agente` | Volver a enviar al agente una solicitud que no le llegó |
| MS C | `GET /api/extracciones/resultados-homologacion` | Tabla `RESULTADO_HOMOLOGACION`: una fila por materia (operador y administrador) |
| MS A (gRPC) | `CatalogoGrpc.ObtenerMallaVigente` / `ResolverCarrera` / `VerificarDocentes` / `ListarDocentes` | Sílabos para el agente / carreras por nombre / revisores válidos / docentes para el registro manual |

## Ejemplos (`documentos-prueba/certificados`, también descargables en la pantalla de registro)

Los revisores de ejemplo (`hnaranjo`, `mvaca`, `jcastro` @uta.edu.ec) se crean como docentes con
`Seed__DocentePassword` (en Docker: `SIHA_DOCENTE_PASSWORD`, o la del administrador).

| PDF | Resultado con los sílabos reales |
|---|---|
| `SIHA-TI-SW-cambio-de-carrera.pdf` | TI → Software. Reprobó 2 materias del segundo nivel. El agente compara cada materia con la regla de 80 % en contenidos y créditos; en las que no llegan (por ejemplo, Programación Orientada a Objetos: 3 créditos frente a 4) recomienda no homologar y deciden los docentes |
| `SIHA-IND-SW-cambio-de-carrera.pdf` | Industrial → Software: materias con temas o créditos por debajo del 80 %: el agente recomienda no homologarlas y deciden los docentes |
| `SIHA-con-errores.pdf` | Rechazado: siglas en la carrera, primer nivel no aprobado, pide una reprobada, materia de hace más de 10 años, cédula y revisor con Gmail |
| `plan-analitico-no-valido.pdf` | Rechazado: no es la plantilla SIHA-CH-01 |

Para regenerarlos: `pip install reportlab` y `python tools/certificados-siha/generar_certificados.py`.

Las solicitudes registradas con el formato anterior del certificado (sin período académico) ya no aparecen en
la lista de Solicitudes: no se pueden analizar con los sílabos.
