# Certificados SIHA-CH-01 de prueba

Generados con `python tools/certificados-siha/generar_pruebas.py`.

| Archivo | Resultado esperado | Qué prueba |
|---|---|---|
| `V01-TEL-SW-cambio-de-carrera.pdf` | Debe registrarse | TEL → SW, 4 materias, revisores Naranjo y Castro |
| `V02-ROB-SW-cambio-de-carrera.pdf` | Debe registrarse | Automatización y Robótica → SW, 5 materias (incluye una reprobada en nivel 2, permitido) |
| `V03-SW-TI-cambio-de-carrera.pdf` | Debe registrarse | SW → TI (dirección inversa), 3 niveles |
| `V04-IND-TI-una-materia.pdf` | Debe registrarse | Industrial → TI con una sola materia (caso mínimo) |
| `V05-IND-SW-dos-materias-a-una.pdf` | Debe registrarse | Industrial → SW: dos materias aprobadas para una sola de destino |
| `V06-movilidad-externa-TI-SW.pdf` | A verificar | Movilidad externa (CAU-P-122) desde otra institución, TI → SW |
| `V07-TI-SW-historial-largo.pdf` | Debe registrarse | TI → SW con historial largo (3 niveles, varias páginas) |
| `E01-cedula-9-digitos.pdf` | Debe rechazarse | Cédula con 9 dígitos |
| `E02-revisor-gmail.pdf` | Debe rechazarse | Revisor 2 con correo Gmail |
| `E03-revisores-repetidos.pdf` | Debe rechazarse | Los dos revisores son la misma persona |
| `E04-revisor-no-existe.pdf` | Debe rechazarse | Revisor con correo UTA que no es docente del sistema |
| `E05-pide-materia-reprobada.pdf` | Debe rechazarse | Pide homologar una materia reprobada |
| `E06-materia-mas-de-10-anios.pdf` | Debe rechazarse | Materia aprobada hace más de 10 años (2014) |
| `E07-primer-nivel-no-aprobado.pdf` | Debe rechazarse | No aprobó todo el primer nivel (Física reprobada) |
| `E08-nota-incoherente.pdf` | Debe rechazarse | Nota 6.50 marcada como Aprobada (incoherente) |
| `E09-carrera-con-siglas.pdf` | Debe rechazarse | Carrera de origen escrita con siglas (TI) |
| `E10-materia-usada-dos-veces.pdf` | Debe rechazarse | La misma materia aprobada usada en dos solicitudes |
| `E11-destino-repetido.pdf` | Debe rechazarse | La misma materia de destino pedida dos veces |
| `E12-ruta-normativa-incorrecta.pdf` | Debe rechazarse | Cambio de carrera con ruta CAU-P-122 (debe ser CAU-P-011) |
| `E13-origen-igual-destino.pdf` | Debe rechazarse | Carrera de origen igual a la de destino |
| `E14-fecha-futura.pdf` | Debe rechazarse | Fecha de emisión en el futuro (2027-01-15) |
| `E15-materia-no-esta-en-historial.pdf` | Debe rechazarse | Solicita una materia que no está en el historial |
| `E16-carrera-no-existe.pdf` | Debe rechazarse | Carrera de destino que no existe en el catálogo (Ingeniería Civil) |
| `E17-sin-solicitudes.pdf` | Debe rechazarse | No pide ninguna homologación |
| `E18-pdf-escaneado.pdf` | Debe rechazarse | PDF escaneado (imagen, sin texto seleccionable) |
| `E19-plan-analitico-suelto.pdf` | Debe rechazarse | No es la plantilla SIHA-CH-01 (plan analítico suelto) |
