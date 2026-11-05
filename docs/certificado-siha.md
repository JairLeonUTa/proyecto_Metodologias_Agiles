# Solicitud con certificado SIHA (formato SIHA-CH-01)

Flujo completo, en pocas pantallas:

1. **Operador**: sube un solo PDF, el certificado de homologación preaprobado por Secretaría.
2. **Agente (MS C)**: en cuanto llega, trae de MS A por gRPC las mallas y sílabos oficiales de la carrera
   de origen y de destino y compara cada materia que se pide homologar en tres criterios (mínimo 80 % cada
   uno): **contenidos**, **profundidad** y **carga horaria**. Si los tres llegan, la materia *cumple*; si no,
   queda **a criterio del docente**. Todo se explica unidad por unidad.
3. **Los dos docentes revisores** que vienen en el PDF ven la solicitud con el análisis ya hecho y deciden
   cada materia (Sí, homologar / No homologar). Queda **homologada** si los dos la aprueban.

## Conocimiento del agente: sílabos oficiales

`src/Services/Siha.MsA.Api/Data/CatalogoUta/*.json` contiene las mallas de Software, Tecnologías de la
Información, Ingeniería Industrial, Telecomunicaciones y Automatización y Robótica, extraídas de los sílabos
publicados en uta.edu.ec (nivel, créditos, horas y cada unidad con sus temas y horas). MS A las carga al
arrancar como malla vigente `UTA-2022`. Para actualizarlas:
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
sola solicitud), materias vigentes (**10 años**), nota coherente con el estado, dos revisores distintos con
correo institucional que existan en el sistema como **DOCENTE activos**, y carreras que existan en el
catálogo. Reprobar materias después del primer período no impide el cambio.

## API

| Servicio | Endpoint | Uso |
|---|---|---|
| MS B | `POST /api/expedientes/certificado/validar` | Vista previa de lo leído |
| MS B | `POST /api/expedientes/certificado` | Registra, guarda el PDF y publica `SolicitudCreadaEvent` |
| MS B | `GET /api/expedientes` | El docente solo recibe las solicitudes donde es revisor |
| MS C | `GET /api/extracciones/expedientes/{id}/analisis` | Historial, reglas, análisis explicado, revisores y resultado final |
| MS C | `POST /api/extracciones/expedientes/{id}/analisis-automatico` | Reintentar si la IA no estaba disponible |
| MS C | `POST /api/extracciones/expedientes/{id}/dictamen` | Decisión del docente revisor (rol DOCENTE) |
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
