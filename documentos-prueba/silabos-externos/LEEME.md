# Sílabos de otra universidad (movilidad externa)

Acompañan al certificado `../certificados-pruebas/V06-movilidad-externa-TI-SW.pdf`
(Escuela Superior Politécnica de Chimborazo, Tecnologías de la Información → Software).
Se adjuntan en la pantalla **Registrar solicitud**, en la sección "Sílabos de la otra
universidad (opcional)" que aparece cuando el certificado es de una movilidad externa.

| PDF | Materia aprobada | Resultado esperado |
|---|---|---|
| `ESPOCH-fundamentos-de-programacion.pdf` | Fundamentos de Programación | Se lee: 4 unidades, 144 horas |
| `ESPOCH-algebra-lineal.pdf` | Álgebra Lineal | Se lee: 4 unidades, 128 horas |
| `ESPOCH-fisica.pdf` | Física | Se lee: 4 unidades, 128 horas |
| `ESPOCH-estructura-de-datos.pdf` | Estructura de Datos | Se lee: 4 unidades, 144 horas |
| `silabo-sin-horas.pdf` | (cualquiera) | Se rechaza: no indica las horas |
| `silabo-sin-unidades.pdf` | (cualquiera) | Se rechaza: no se encontraron unidades |

Una materia sin sílabo adjunto sigue mostrando "Sin sílabo para comparar" y la deciden
los docentes revisores; no cuenta como que falló el 80 %.

Para regenerarlos: `pip install reportlab` y `python tools/certificados-siha/generar_silabos.py`.
