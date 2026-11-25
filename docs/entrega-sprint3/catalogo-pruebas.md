# Catálogo de pruebas para el informe (Sprints 1, 2 y 3)

Cada prueba se ejecuta en el sistema actual (rama `fix/bloqueo-creditos`) y lleva una captura en `capturas/`.
Formato en el informe: **PA-xx.y: título** · Acción · Figura · Resultado obtenido · Estado.
Estados: Cumple · Cumple (automática) · Reemplazada (corrección C-0x) · Pendiente.

## Sprint 1 (HU-01 a HU-06)
| PA | Historia | Prueba | Rol | Captura |
|---|---|---|---|---|
| PA-01.1 | HU-01 Cuentas y roles | Lista de cuentas con su rol y estado | Admin | pa01-1-cuentas.png |
| PA-01.2 | HU-01 | Crear cuenta: correo no institucional / contraseñas distintas se rechazan | Admin | pa01-2-validacion.png |
| PA-01.3 | HU-01 | Rol sin permiso entra a Usuarios → "No tiene acceso a esta sección" | Operador | pa01-3-acceso-denegado.png |
| PA-02.1 | HU-02 Reglas y umbrales | Reglas de homologación explicadas | Admin | pa02-1-reglas.png |
| PA-02.2 | HU-02 | Configurar mínimo: no deja bajar de 80 % | Admin | pa02-2-minimo.png |
| PA-03.1 | HU-03 Carreras y mallas | Carreras y versiones de malla | Admin | pa03-1-mallas.png |
| PA-03.2 | HU-03 | Asignaturas con créditos, horas y unidades | Admin | pa03-2-asignaturas.png |
| PA-04.1 | HU-04 Gateway | Petición sin JWT → 401 | — | pa04-1-401.png |
| PA-04.2 | HU-04 | Docente pide un recurso de administrador → 403 | Docente | pa04-2-403.png |
| PA-05.1 | HU-05 Registro de solicitudes | Archivo que no es el certificado → rechazado | Operador | pa05-1-rechazo.png |
| PA-05.2 | HU-05 | Certificado válido registrado con expediente | Operador | pa05-2-registro.png |
| PA-06.1 | HU-06 Aviso a otros servicios | Cola de RabbitMQ y evento publicado | Admin RabbitMQ | pa06-1-rabbitmq.png |

## Sprint 2 (HU-07 a HU-11) — mismas pruebas del documento del Sprint 2
| PA | Prueba (Sprint 2) | En el sistema actual | Captura |
|---|---|---|---|
| PA-07.1 | Lectura de un PDF con texto | Datos leídos del certificado SIHA | pa07-1 |
| PA-07.2 | Aviso repetido | Certificado ya registrado → aviso con enlace (y prueba automática de eventos) | pa07-2 |
| PA-07.3 | Lectura poco segura | Certificado con campos sin llenar → checklist ✘ | pa07-3 |
| PA-07.4 | Confirmar con datos vacíos | Registro manual con datos faltantes → rechazado | pa07-4 |
| PA-07.5 | PDF escaneado | "Este PDF no es el certificado SIHA" | pa07-5 |
| PA-08.1 | Reglas en el resultado | Análisis muestra el mínimo vigente (80 %) | pa08-1 |
| PA-08.2 | Cambio de umbral | Admin sube a 85 %; no deja bajar de 80 (se restaura 80) | pa08-2 |
| PA-08.3 | Servicio de reglas/IA apagado | "El agente se está preparando" y reintento | pa08-3 |
| PA-09.1 | Créditos suficientes | Materia con 100 % de créditos | pa09-1 |
| PA-09.2 | Créditos insuficientes (2 vs 3) | Álgebra Lineal 66,67 % | pa09-2 |
| PA-09.3 | Destino sin créditos | Prueba automática | — |
| PA-09.4 | Materia de más de 10 años | Certificado E06 rechazado | pa09-4 |
| PA-09.5 | Tercera matrícula | Regla "vigentes y sin tercera matrícula" en el análisis | pa09-5 |
| PA-10.1 | Escaneado legible | Reemplazada (C-01): solo se acepta el certificado digital | — |
| PA-10.2 | Cobertura por unidades | Tabla de comparación tema por tema | pa10-2 |
| PA-10.3 | Parecido bajo el umbral | Unidad marcada como no cubierta | pa10-3 |
| PA-10.4 | Dos materias para un destino | Certificado V05 | pa10-4 |
| PA-10.5 | Misma materia en dos destinos | Certificado E10 rechazado | pa10-5 |
| PA-11.1 a 11.5 | Precedentes | Según lo que muestre el análisis + pruebas automáticas | pa11-x |

## Sprint 3 (HU-12 a HU-19)
| PA | Historia | Prueba | Captura |
|---|---|---|---|
| PA-12.1 | HU-12 Certificado único | Datos leídos y registro | pa12-1 |
| PA-12.2 | HU-12 | Revisión previa con checklist ✔/✘ | pa12-2 |
| PA-12.3 | HU-12 | Certificado repetido → aviso junto al botón y enlace | pa12-3 |
| PA-13.1 | HU-13 Análisis explicado | Análisis materia por materia con su porqué | pa13-1 |
| PA-13.2 | HU-13 | "El agente se está preparando" | pa13-2 |
| PA-14.1 | HU-14 Movilidad externa | Solicitud externa con sílabo de otra universidad | pa14-1 |
| PA-15.1 | HU-15 Regla 80 % | Créditos < 80 % → bloqueada, sin voto | pa15-1 |
| PA-15.2 | HU-15 | Solo contenidos < 80 % → recomendación votable | pa15-2 |
| PA-16.1 | HU-16 Decisión | Docente vota | pa16-1 |
| PA-16.2 | HU-16 | Sin acuerdo → "Falta la decisión del administrador" | pa16-2 |
| PA-16.3 | HU-16 | Administrador decide con motivo | pa16-3 |
| PA-16.4 | HU-16 | Votos cerrados (pantalla y 409) | pa16-4 |
| PA-17.1 | HU-17 Tabla de resultados | RESULTADO_HOMOLOGACION | pa17-1 |
| PA-18.1 | HU-18 Registro manual | Validar y registrar sin PDF | pa18-1 |
| PA-19.1 | HU-19 Cuentas y sesión | Restablecer contraseña | pa19-1 |
| PA-19.2 | HU-19 | Después de iniciar sesión no cae en una sección prohibida | pa19-2 |
