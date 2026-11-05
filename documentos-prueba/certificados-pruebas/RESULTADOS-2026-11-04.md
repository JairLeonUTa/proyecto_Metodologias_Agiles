# Resultados de pruebas — Homologa UTA (04/11/2026)

Probado en http://localhost:5173 (frontend) con la API en localhost, usuario admin@uta.edu.ec.
26 certificados subidos desde la pantalla "Registrar solicitud". **26/26 con el resultado esperado.**

## Válidos (7/7 registrados y analizados por el agente)

| PDF | Expediente | Revisores | Análisis del agente |
|---|---|---|---|
| V01 TEL → SW | EXP-20261105-032AE5 | Naranjo, Castro | 4 materias: 1 cumple, 3 a criterio |
| V02 ROB → SW | EXP-20261105-C37E2B | Vaca, Castro | 5 materias: 0 cumplen, 5 a criterio |
| V03 SW → TI | EXP-20261105-591371 | Naranjo, Vaca | 4 materias: 3 cumplen, 1 a criterio |
| V04 IND → TI (1 materia) | EXP-20261105-B5C975 | Naranjo, Vaca | 1 materia: 0 cumplen, 1 a criterio |
| V05 IND → SW (2 materias → 1) | EXP-20261105-0D172B | Castro, Naranjo | 3 materias: 2 cumplen (incluida la combinación Cálculo Dif. + Int.), 1 a criterio |
| V06 Movilidad externa ESPOCH | EXP-20261105-079F94 | Naranjo, Vaca | 4 a criterio: sin sílabo de otra institución (correcto) |
| V07 TI → SW historial largo | EXP-20261105-64DB59 | Vaca, Naranjo | 10 materias: 7 cumplen, 3 a criterio; leyó las 16 materias del historial |

Todas quedaron en "Revisión docente 0/2".

## Inválidos (19/19 rechazados, sin botón de registrar)

| PDF | Mensaje mostrado |
|---|---|
| E01 | La cédula '180411122' debe tener 10 dígitos. |
| E02 | El correo del docente revisor 2 (pruiz@gmail.com) debe ser institucional (@uta.edu.ec). |
| E03 | Los dos docentes revisores deben ser personas distintas. |
| E04 | El docente revisor Ing. Laura García (lgarcia@uta.edu.ec) no está registrado en el sistema. |
| E05 | Solicitud 4: 'Medidas Eléctricas' está reprobada; solo se pueden homologar materias aprobadas. |
| E06 | Materia 'Realidad Nacional': se aprobó en Abr-Sep 2014, hace más de 10 años… |
| E07 | No aprobó todo el primer período académico (Física)… |
| E08 | Materia 'Lógica Matemática': figura como Aprobada pero su nota 6.5 es menor que 7. |
| E09 | 'Carrera de origen' debe escribirse con el nombre completo… (se leyó 'TI'). |
| E10 | Solicitud 4: 'Física' ya se usa en otra solicitud… |
| E11 | La materia de destino 'Física' se pide más de una vez. |
| E12 | Un cambio de carrera debe usar la ruta CAU-P-011. |
| E13 | En un cambio de carrera la carrera de origen y la de destino deben ser distintas. |
| E14 | La fecha de emisión está en el futuro. |
| E15 | Solicitud 4: 'Sistemas Digitales' no está en el historial académico. |
| E16 | La carrera de destino 'Ingeniería Civil' no existe en el catálogo de la UTA. |
| E17 | El certificado no indica qué materia quiere homologar… |
| E18 | El PDF no tiene texto seleccionable (parece escaneado)… |
| E19 | El documento no corresponde a la plantilla oficial SIHA (SIHA-CH-01)… |

## Observaciones

1. **La sesión se cierra a los 60 min** (`Jwt:ExpiraMinutos = 60`) y no hay renovación del token: en medio de una revisión,
   el siguiente 401 manda al login. Considerar refresh token o avisar antes de que expire.
2. En E08 la nota sale como "6.5" y "7", mientras que el resto de la UI usa "6,50" (coma decimal y dos decimales).
3. En V06 (movilidad externa), la sección de reglas muestra "contenidos 0 %, profundidad 0 %, carga horaria 0 %";
   sería más claro decir "sin sílabo para comparar", como ya lo dice el detalle de cada materia.
4. La solicitud antigua de "Pérez Gómez Ana Lucía" (EXP-20261104-E46994) aparece como "IND → SW" con 0 materias pedidas;
   parece un registro de una versión anterior. No se modificó.
