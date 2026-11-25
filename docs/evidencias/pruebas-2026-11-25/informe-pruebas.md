# Informe de pruebas — SIHA-IA (Homologa UTA)

| Dato | Valor |
|---|---|
| Fecha | 25/11/2026 |
| Responsable | Jonathan Chisag |
| Rama probada | `fix/bloqueo-creditos` (incluye `fix/reanalizar-y-certificados-anteriores`) |
| Commit | `1d8798c` (CP-01 a CP-09) y corrección de H-01 (CP-10) |
| Entorno | Docker Compose, perfil `sprint23` (Gateway, MS A, MS B, MS C, SQL Server ×3, RabbitMQ, Ollama, frontend) |
| Navegador | Playwright (Chromium), una sesión aislada por rol |

## 1. Objetivo

Comprobar de extremo a extremo la regla de homologación aclarada con el docente y las funciones nuevas:

1. **Los créditos por debajo del 80 % bloquean** la homologación (sin votación).
2. **Los contenidos por debajo del 80 % solo aconsejan**: los dos docentes revisores votan y deciden.
3. **Si los docentes no se ponen de acuerdo**, decide el Administrador con un motivo obligatorio, y después ya no se
   pueden cambiar los votos.
4. Cada paso queda registrado en las bases de datos de cada microservicio.

## 2. Datos de prueba

| Elemento | Valor |
|---|---|
| Certificado | `V02-ROB-SW-cambio-de-carrera.pdf` (SIHA-2026-0202) |
| Estudiante | Benítez Lara Camila Fernanda |
| Trámite | Cambio de carrera: Automatización y Robótica → Software (CAU-P-011) |
| Docentes revisores | Ing. Carlos Paredes (`cparedes@uta.edu.ec`), Ing. María Vaca (`mvaca@uta.edu.ec`) |
| Operador | `operador@uta.edu.ec` |
| Administrador | `admin@uta.edu.ec` |

## 3. Pruebas automáticas (antes de la prueba manual)

| Conjunto | Resultado |
|---|---|
| MS A (xUnit) | 130 correctas |
| MS B (xUnit) | 158 correctas |
| MS C (xUnit) | 257 correctas |
| Frontend (Vitest) | 90 correctas |
| ESLint y TypeScript | Sin errores |

Pruebas nuevas relevantes en MS C: Física 2 frente a 3 créditos bloqueada; contenidos 60 % con créditos 100 % que se
homologa con dos votos; decisión del Administrador sobre una materia bloqueada rechazada; análisis guardado antes del
cambio también bloqueado; con un solo voto no hay decisión final; votos cerrados tras la decisión final; la solicitud
queda RESUELTA solo cuando ya no hay materias sin acuerdo.

## 4. Casos de prueba manuales

| ID | Rol | Acción | Resultado esperado | Resultado obtenido | Estado | Evidencia |
|---|---|---|---|---|---|---|
| CP-01 | Operador | Subir V02 y registrar | Checklist sin errores; solicitud creada y enviada al agente | Datos leídos correctamente (5 materias, revisores Vaca y Paredes). Se creó **EXP-20261125-8EC017** y se envió al agente | ✅ | [lectura](cp01-certificado-leido.png), [registro](cp01-registrada.png) |
| CP-02 | Sistema | Análisis del agente | Cada materia con % de contenidos y créditos; las de créditos < 80 % marcadas "No se puede homologar" | Estado ANALIZADO. Bloqueadas por créditos: Algoritmos (cont. 68,75 %, créd. 50 %), POO (75 % / 50 %), Álgebra Lineal (100 % / 66,67 %). Cumplen: Cálculo Diferencial y Metodología (100 % / 100 %) | ✅ | [análisis](cp02-analisis-agente.png) |
| CP-03 | Docente Paredes | Votar las materias no bloqueadas | Voto guardado; estado "En revisión" | Solo aparecen para votar Cálculo Diferencial y Metodología. Aprobó ambas con comentario; estado "Revisión docente 1/2" | ✅ | [voto](cp03-voto-paredes.png) |
| CP-04 | Docente Vaca | Votar distinto en una materia | Esa materia "Sin acuerdo"; solicitud "Falta la decisión del administrador" | Rechazó Cálculo Diferencial y aprobó Metodología. La solicitud pasó a "Falta la decisión del administrador" y los docentes ven el aviso del desacuerdo | ✅ | [sin acuerdo](cp04-sin-acuerdo-docente.png) |
| CP-05 | Administrador | Decidir sin motivo | No deja guardar | El administrador ve "Los dos docentes no se pusieron de acuerdo en: Cálculo Diferencial". Con "Sí, homologar" elegido y sin motivo, "Guardar decisión final" sigue deshabilitado | ✅ | [sin motivo](cp05-sin-motivo.png) |
| CP-06 | Administrador | Decidir con motivo | Materia con resultado final; solicitud "Resuelta" | Guardó "homologar" con motivo. Cálculo Diferencial: Homologada por decisión del administrador (se muestra el motivo); Metodología: Homologada (2 votos); las 3 bloqueadas: No homologadas. Solicitud "Resuelta" | ✅ | [antes](cp06-decision-admin-antes.png), [guardada](cp06-decision-admin-guardada.png) |
| CP-07 | Docente | Cambiar su voto después | Aviso "El administrador ya dio la decisión final…" | La pantalla de María Vaca ya no muestra botones de voto y dice "El administrador ya dio la decisión final.". Intento directo a la API (`POST …/dictamen`) con su sesión → **409** "El administrador ya dio la decisión final de esta solicitud; los votos ya no se pueden cambiar." | ✅ | [voto cerrado](cp07-voto-cerrado.png) |
| CP-08 | Docente | Intentar votar una materia bloqueada por créditos | No hay botones de voto | En "Decisión de los docentes" solo hay botones para las 2 materias que cumplen; las 3 bloqueadas muestran "No se puede homologar" | ✅ | [análisis](cp02-analisis-agente.png), [voto](cp03-voto-paredes.png) |
| CP-10 | Operador / Docente | Registrar V03 (SIHA-2026-0203) y revisar una materia con **solo contenidos** bajos | Resumen: "no llega al 80 % en contenidos y el agente recomienda no homologarla"; la materia **sí** se puede votar | Sistemas Operativos: contenidos 75 %, créditos 100 % → NO_CUMPLE sin bloqueo. El resumen usa el texto nuevo (corrección de H-01) y el docente Paredes tiene botones "Sí, homologar / No homologar" para ella | ✅ | [resumen](cp10-resumen-solo-contenidos.png), [voto](cp10-voto-solo-contenidos.png) |
| CP-09 | Base de datos | Revisar tablas de MS B y MS C | Expediente, análisis, votos, decisión final y `RESULTADO_HOMOLOGACION` coherentes | MS B: `EXPEDIENTE` con SIHA-2026-0202 y evento publicado; `EXPEDIENTE_DOC` con el PDF VALIDADO. MS C: `ANALISIS_EXPEDIENTE` ANALIZADO; `DICTAMEN_DOCENTE` con los 2 votos y comentarios; `DECISION_FINAL` con la decisión del administrador; `RESULTADO_HOMOLOGACION` con 5 filas coherentes (ver tabla abajo) | ✅ | [consultas](cp09-tablas.txt) |

## 5. Evidencias

Las capturas y la salida de las consultas están en esta misma carpeta y se enlazan en la tabla de casos.

### Resultado final en la base de datos (`SihaDbC.RESULTADO_HOMOLOGACION`, EXP-20261125-8EC017)

| Materia de destino | Contenidos | Créditos | Agente | Sí | No | Resultado final | Por qué |
|---|---|---|---|---|---|---|---|
| Algoritmos y Lógica de Programación | 68,75 % | 50,00 % | NO_CUMPLE | 0 | 0 | NO_HOMOLOGADA | Créditos < 80 %: bloqueada, sin votación |
| Programación Orientada a Objetos | 75,00 % | 50,00 % | NO_CUMPLE | 0 | 0 | NO_HOMOLOGADA | Créditos < 80 %: bloqueada, sin votación |
| Álgebra Lineal | 100,00 % | 66,67 % | NO_CUMPLE | 0 | 0 | NO_HOMOLOGADA | Créditos < 80 % aunque cubre todos los temas (ejemplo del docente) |
| Cálculo Diferencial | 100,00 % | 100,00 % | CUMPLE | 1 | 1 | HOMOLOGADA | Docentes sin acuerdo → decidió el Administrador con motivo |
| Metodología de la Investigación | 100,00 % | 100,00 % | CUMPLE | 2 | 0 | HOMOLOGADA | Los dos docentes aprobaron |

### Recorrido por microservicio

```
Operador ─PDF─► Gateway ─► MS B  lee y valida el certificado → EXPEDIENTE, EXPEDIENTE_DOC
                              │  RabbitMQ: SolicitudCreadaEvent
                              ▼
                             MS C  compara sílabos (MS A + Ollama) → ANALISIS_EXPEDIENTE
Docentes ─votos─► Gateway ─► MS C  → DICTAMEN_DOCENTE
Administrador ─decisión─► Gateway ─► MS C → DECISION_FINAL → RESULTADO_HOMOLOGACION
```

## 6. Hallazgos

| ID | Gravedad | Descripción | Dónde |
|---|---|---|---|
| H-01 | Baja (texto) — **corregido** | El resumen del paso 1 decía que las 3 materias "no llegan al 80 % y el agente recomienda no homologarlas", y el paso 3 decía "recomienda no homologar" para materias **bloqueadas por créditos**. **Corrección:** el agente ahora separa los casos: "N no se puede(n) homologar porque los créditos no llegan al 80 %" y "N no llega(n) al 80 % en contenidos y el agente recomienda no homologarla(s)". Verificado con V03 (CP-10) y con pruebas automáticas. Los análisis hechos antes de la corrección (como V02) conservan el texto anterior porque se guarda al analizar. | `AnalisisAutomaticoService.cs` (resumen y regla de equivalencia) |
| H-02 | Media (diseño) | El menú del Operador ya no muestra "Reglas de homologación" (se reemplazó por "Registro manual"). | Menú lateral |
| H-03 | Baja (dato) | En MS B el expediente sigue en `PENDIENTE_CONTENIDOS` aunque en MS C ya está resuelto: la decisión final vive solo en MS C. | `SihaDbB.EXPEDIENTE.Estado` |
| H-04 | Baja (dato) | Mientras el administrador no decide, los docentes todavía pueden cambiar su voto ("Puede cambiar su decisión"). Es el comportamiento esperado; se cierra al haber decisión final. | Detalle de la solicitud |

## 7. Conclusión

Los **10 casos de prueba se aprobaron** (10/10). El sistema aplica la regla aclarada con el docente:

- Las materias con **créditos por debajo del 80 %** quedan bloqueadas ("No se puede homologar"), sin botones de voto y
  con resultado NO_HOMOLOGADA, incluso cuando cubren todos los temas (Álgebra Lineal, 66,67 %).
- Las materias que **cumplen** las votan los dos docentes.
- Las materias con **solo contenidos bajos** (créditos ≥ 80 %) quedan como recomendación de "no homologar", pero los
  docentes **sí** las pueden votar (Sistemas Operativos, 75 % / 100 %).
- Cuando los docentes **no coinciden**, la solicitud muestra "Falta la decisión del administrador"; el Administrador decide
  con un **motivo obligatorio**, la solicitud pasa a "Resuelta" y los **votos quedan cerrados** tanto en la pantalla como
  en el servidor (409).
- Cada paso queda registrado en la base de su microservicio y el resultado final por materia queda en
  `RESULTADO_HOMOLOGACION`.

De los 4 hallazgos, **H-01 ya se corrigió** durante la prueba. Los otros (H-02 a H-04) son menores y ninguno impide el
flujo; se recomienda decidir con el equipo H-02 (volver a mostrar "Reglas de homologación" en el menú) antes de unir la
rama a `develop`.
