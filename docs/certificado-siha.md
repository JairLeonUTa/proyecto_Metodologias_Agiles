# Solicitud con certificado SIHA (formato SIHA-CH-01)

Flujo completo, en pocas pantallas:

1. **Operador**: sube un solo PDF, el certificado de homologación preaprobado por Secretaría.
2. **Agente (MS C)**: en cuanto llega, pide a MS A por gRPC la malla vigente de la carrera de destino,
   compara contenidos y guarda la estimación. Con **80 % o más** la materia *cumple*; con menos, queda
   **a criterio del docente**. Solo compara materias con contenidos afines (al menos una unidad parecida):
   no intenta homologar Programación I con Física I.
3. **Los dos docentes revisores** que vienen en el PDF ven la solicitud con el análisis ya hecho y responden
   materia por materia (aprobar / no aprobar). Queda **homologada** si los dos la aprueban; si discrepan,
   *sin acuerdo*.

## Qué debe traer el PDF

Texto seleccionable (no escaneado) con etiquetas `Etiqueta: valor` (tildes y mayúsculas no importan).

| Sección | Etiquetas |
|---|---|
| Encabezado | `CERTIFICADO DE HOMOLOGACIÓN` y el código `SIHA-CH-01` |
| Certificado | `Número de certificado`, `Fecha de emisión`, `Emitido por` |
| Estudiante | `Estudiante`, `Cédula` (10 dígitos) |
| Trámite | `Tipo de trámite` (INTERNO/EXTERNO), `Ruta normativa` (CAU-P-011/CAU-P-122), `Institución de origen`, `Carrera de origen` y `Carrera de destino` como `CÓDIGO - Nombre` |
| Revisores | `Docente revisor 1` y `Docente revisor 2` como `Nombre - correo@uta.edu.ec` |
| Cada materia | `Asignatura: CÓDIGO - Nombre`, `Semestre`, `Créditos`, `Horas totales`, `Fecha de aprobación`, `Nota` (0–10, mínimo 7); luego `Unidad N: nombre (H horas)` y `Contenidos: ...` |
| Cierre | `FIN DEL CERTIFICADO` (firmas y sellos posteriores no se leen) |

El certificado ya viene filtrado por Secretaría: **no incluye terceras matrículas ni materias de hace más de
10 años**. Por eso no hay campo de matrícula y una materia con más de 10 años hace que se rechace.

Se rechaza con la lista completa de errores si falta un dato, si INTERNO no usa CAU-P-011 (o EXTERNO no
usa CAU-P-122), si una nota no es aprobatoria, si las horas de las unidades no suman las horas totales, si
los revisores no son dos personas distintas con correo institucional, **si algún revisor no existe en el
sistema como usuario activo con rol DOCENTE** (MS B lo verifica en MS A por gRPC) o si el certificado ya
se registró.

## API

| Servicio | Endpoint | Uso |
|---|---|---|
| MS B | `POST /api/expedientes/certificado/validar` | Vista previa de lo leído |
| MS B | `POST /api/expedientes/certificado` | Registra, guarda el PDF y publica `SolicitudCreadaEvent` |
| MS B | `GET /api/expedientes` | El docente solo recibe las solicitudes donde es revisor |
| MS C | `GET /api/extracciones/expedientes/{id}/analisis` | Estimación, revisores, respuestas y resultado final |
| MS C | `POST /api/extracciones/expedientes/{id}/analisis-automatico` | Reintentar si la IA no estaba disponible |
| MS C | `POST /api/extracciones/expedientes/{id}/dictamen` | Respuesta del docente revisor (rol DOCENTE) |
| MS A (gRPC) | `CatalogoGrpc.ObtenerMallaVigente` / `VerificarDocentes` | Malla para el agente / revisores válidos |

El contenido mínimo (80 %) se configura en **Reglas** (solo administrador). La comparación por unidad
(parecido semántico) sigue siendo interna y no se muestra.

## Ejemplos (`documentos-prueba/certificados`, también descargables en la pantalla de registro)

Los revisores de los ejemplos (`hnaranjo`, `mvaca`, `jcastro` @uta.edu.ec) se crean como docentes al
arrancar MS A con `Seed__DocentePassword` (en Docker: `SIHA_DOCENTE_PASSWORD`, o la del administrador).

| PDF | Resultado esperado |
|---|---|
| `SIHA-IND-SW-completo.pdf` | SW105 Física I ← IND105 Física I + IND205 Física II (cumple); SW101 ← IND108 (cumple) |
| `SIHA-IND-SW-solo-fisica1.pdf` | SW105 queda **a criterio del docente** (Física I de Industrial cubre ~62 %); SW101 cumple |
| `SIHA-UCuenca-SW-externo.pdf` | Trámite externo CAU-P-122: SW101 y SW201 cumplen |
| `SIHA-con-errores.pdf` | Rechazado: cédula, revisor con Gmail, nota 6,40, horas que no cuadran y materia de hace más de 10 años |
| `plan-analitico-no-valido.pdf` | Rechazado: no es la plantilla SIHA-CH-01 |

Para regenerarlos: `pip install reportlab` y `python tools/certificados-siha/generar_certificados.py`.
