# Certificado de homologación SIHA (formato SIHA-CH-01)

La solicitud se registra con **un solo PDF**: el certificado de homologación preaprobado por
Secretaría. El operador no digita carreras, materias, años ni matrículas; todo sale del documento.

## Qué debe traer el PDF

Texto seleccionable (no escaneado) con estas etiquetas `Etiqueta: valor`. Las tildes y mayúsculas no
importan y varias etiquetas pueden ir en la misma línea.

| Sección | Etiquetas |
|---|---|
| Encabezado | `CERTIFICADO DE HOMOLOGACIÓN` y el código `SIHA-CH-01` |
| Certificado | `Número de certificado`, `Fecha de emisión`, `Emitido por` |
| Estudiante | `Estudiante`, `Cédula` (10 dígitos) |
| Trámite | `Tipo de trámite` (INTERNO/EXTERNO), `Ruta normativa` (CAU-P-011/CAU-P-122), `Institución de origen`, `Carrera de origen` y `Carrera de destino` como `CÓDIGO - Nombre` |
| Cada materia | `Asignatura: CÓDIGO - Nombre`, `Semestre`, `Créditos`, `Horas totales`, `Fecha de aprobación` (AAAA-MM-DD o DD/MM/AAAA), `Nota` (0–10, mínimo 7), `Matrícula`; luego `Unidad N: nombre (H horas)` y `Contenidos: ...` |
| Cierre | `FIN DEL CERTIFICADO` (lo que sigue, firmas y sellos, no se lee) |

Se rechaza con la lista completa de errores si falta un dato, si INTERNO no usa CAU-P-011 (o EXTERNO
no usa CAU-P-122), si la nota no es aprobatoria, si las horas de las unidades no suman las horas
totales, si la fecha de aprobación es posterior a la emisión o si el certificado ya se registró.

## Flujo

1. `POST /api/expedientes/certificado/validar` (MS B): lee el PDF y devuelve lo que entendió (vista previa).
2. `POST /api/expedientes/certificado` (MS B): crea el expediente con estudiante, carreras y materias
   (semestre, créditos, horas, fecha, nota, matrícula), guarda el PDF y publica `SolicitudCreadaEvent`.
3. MS C reconoce el certificado y crea una extracción **CONFIRMADA** (confianza 1,00) por materia.
4. `POST /api/extracciones/expedientes/{id}/analisis-automatico` (MS C): compara todas las materias
   aprobadas con todas las de la carrera de destino y propone solo las que alcanzan el contenido mínimo
   (θ vigente por gRPC). Varias materias pueden cubrir una (N:1) y cada materia aprobada se usa una sola
   vez (RN-19). θ, δ y τ siguen decidiendo en el servidor y se configuran en Reglas, pero no se muestran
   en las pantallas de registro ni de análisis.

## Ejemplos (`documentos-prueba/certificados`, descargables desde la pantalla de registro)

| PDF | Resultado esperado |
|---|---|
| `SIHA-IND-SW-completo.pdf` | SW105 Física I ← IND105 Física I + IND205 Física II (100 %); SW101 ← IND108 |
| `SIHA-IND-SW-solo-fisica1.pdf` | SW105 **no** se homologa (Física I de Industrial cubre ~62 %, faltan fluidos, calor y electricidad); SW101 pide validación de conocimientos (11 años) |
| `SIHA-UCuenca-SW-externo.pdf` | Trámite externo CAU-P-122: SW101 y SW201 |
| `SIHA-con-errores.pdf` | Rechazado: cédula, nota 6,40 y horas que no cuadran |
| `plan-analitico-no-valido.pdf` | Rechazado: no es la plantilla SIHA-CH-01 |

Para regenerarlos: `pip install reportlab` y `python tools/certificados-siha/generar_certificados.py`.
