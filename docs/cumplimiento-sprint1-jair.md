# Cumplimiento de la planificación · Sprint 1 · Jair

## Fuente principal y alcance

**Informe técnico y planificación por sprints SIHA-IA**, 66 páginas, texto y páginas
compartidos por el usuario en esta conversación. Se contrastan §2.1–2.2 (pp. 3–4),
§5.1 (pp. 11–29) y arquitectura de pp. 65–66. Esta fuente sustituye como referencia
principal a la interpretación secundaria de Paul; las evidencias anteriores se
conservan como registro de lo comprobado en aquel momento, no como aceptación final.

El alcance de esta corrección sigue siendo **S1-J01 a S1-J10 en `develop`**, con sus
dependencias de US-01 a US-06. No se adelantan OCR, embeddings, motor de evaluación,
consumidor productivo de C, clasificación, precedentes o informes de Sprints 2–3.

| Tarea | Exigencia de la planificación | Referencia |
|---|---|---|
| S1-J01 | Contratos REST de usuarios/reglas/catálogo y migraciones iniciales USUARIO/ROL DB A | p. 11 |
| S1-J02 | Alta/actualización de usuarios, BCrypt, correo institucional único y estado/rol | p. 12 |
| S1-J03 | Consulta y actualización de reglas/umbrales por ruta, rangos y administrador | p. 15 |
| S1-J04 | CRUD de carreras y versiones de malla, códigos únicos y relaciones DB A | p. 18 |
| S1-J05 | CRUD de asignaturas/unidades e importador de 43 planes estructurados | p. 19 |
| S1-J06 | Gateway con rutas y permisos JWT/RBAC en el borde | p. 21 |
| S1-J07 | MassTransit/RabbitMQ en B y publicación de solicitud válida | p. 28 |
| S1-J08 | Persistencia vinculada a publicación, reintento y trazabilidad | p. 28 |
| S1-J09 | Recorrido Gateway → DB B → RabbitMQ con respuesta 202 | p. 28 |
| S1-J10 | Corrección de hallazgos, revisión técnica y smoke del Sprint 1 | p. 22 |

## Criterios de contraste

- **US-01, p. 11:** hash BCrypt no expuesto; login de cuenta activa y JWT con rol;
  cuenta inactiva/credenciales incorrectas 401; gestión solo administrador, otros 403;
  correo duplicado 409.
- **US-02, p. 15:** cada regla tiene trámite, versión, fuente y vigencia; normativa
  separada de heurísticas; valores inválidos y `delta > theta` dan 400 sin mutación;
  docente/operador no modifican reglas.
- **US-03, p. 18:** una consulta identifica carrera, versión/fuente del plan,
  asignatura, créditos, horas y unidades. Las referencias de origen aprobado y destino
  se conservan en el expediente. Duplicados rechazados; plan incompleto pendiente.
  La disponibilidad de los 43 planes es dependencia de insumo: no sustituir faltantes
  con datos inventados ni certificar un lote sintético como catálogo institucional completo.
- **US-04, p. 21:** reenviar la respuesta real; JWT ausente/vencido/inválido 401,
  rol insuficiente 403, ruta desconocida 404 y servicio indisponible 503.
- **US-05, p. 24:** cambio de carrera/universidad corresponde a CAU-P-011 y movilidad
  a CAU-P-122. Interno/externo no sustituye esa distinción. Vincular materias/documentos,
  rechazar entradas/archivos inválidos y no presentarlos como listos.
- **US-06, p. 27:** publicar identificador y referencias, no binarios; solicitud
  aceptada para procesamiento devuelve 202/PENDIENTE_CONTENIDOS, nunca evaluada;
  fallo del bus deja estado recuperable y reintento conserva CorrelationId.

## Precisiones normativas y límites de sprint

- RN-01: CAU-P-011 exige **80 % en cada dimensión** de contenido, profundidad y carga
  horaria; no equivale a 80 % de créditos ni al theta configurable.
- RN-02: CAU-P-122 exige 80 % de resultados de aprendizaje **solo para movilidad**.
- RN-09/RN-10: los documentos exigidos son los enumerados en p. 4. Validar un PDF
  genérico no demuestra que se hayan aportado todas esas evidencias institucionales.
- RN-13: créditos son **dato de apoyo**, sin atribuir un mínimo legal al 80 %.
- RN-14: cobertura ponderada por horas documentadas; si faltan datos, pendiente.
  El corte de similitud coseno `tau` no es un mínimo del porcentaje de cobertura.
- RN-15: 48 h/crédito solo para registros UTA sustentados. No convertir automáticamente
  créditos externos. El cálculo/confirmación externos se implementan en Sprint 2.
- Modelo p. 3: theta=80 %, delta=15 %, tau=0,75. Si el contrato REST/gRPC existente
  conserva tau porcentual, **75 debe transformarse a 0,75 dividiendo entre 100** en
  el consumidor de similitud; no comparar cosenos con 75 ni cobertura con tau.
- **S1-P04, p. 27:** para Sprint 1 la recepción se demuestra con un **suscriptor de
  prueba RabbitMQ**, sin activar análisis de C. El consumidor productivo idempotente
  y su descarga autorizada pertenecen a **S2-J01, p. 31**. No se exigen para cerrar S1.
- La revisión integral de las **19 RN** es **S3-P09, p. 60**. Registrar metadatos en A
  no implica ejecutar ya todos sus controles de Sprints 2–3.

## Estado de verificación

Contraste en curso. Los resultados de `evidencias/correcciones-jair-20261020.md`
son la línea base previa: no demuestran por sí solos todos los criterios anteriores.
Se añadirá aquí la trazabilidad final de correcciones y verificaciones nuevas,
distinguiendo pruebas unitarias, HTTP aislado, SQL/RabbitMQ y dependencias pendientes.

### Estado actual (20-oct-2026)

| Tarea | Criterio PDF | Implementado | Verificado | Pendiente |
|---|---|---|---|---|
| S1-J01 | Contratos/migraciones USUARIO/ROL DB A | Sí | Compilación + tests | SQL real |
| S1-J02 | Alta/actualización usuarios, BCrypt, email único, estado/rol | Sí | 113/113 MsA | SQL real concurrencia |
| S1-J03 | Reglas/umbrales por ruta, rangos, admin, normativa/heurística | Sí | 113/113 MsA | SQL real |
| S1-J04 | CRUD carreras/mallas, códigos únicos, relaciones DB A | Sí | 113/113 MsA | SQL real |
| S1-J05 | CRUD asignaturas/unidades, importador 43 planes, validación común | Parcial* | 113/113 MsA | Consulta integrada (CA1), referencias inequívocas (CA2), procedencia externa (RN15), faltantes pendientes (RN14) |
| S1-J06 | Gateway rutas/permisos JWT/RBAC, 503 indisponible | Sí | 26 checks HTTP | SQL/RabbitMQ real, token vencido/firma inválida |
| S1-J07 | MassTransit/RabbitMQ en B, publicación solicitud válida | Sí | 66/66 MsB, integración real previa | Integración real actualizada |
| S1-J08 | Persistencia vinculada a publicación, reintento, trazabilidad | Sí | 66/66 MsB, integración real previa | Integración real actualizada |
| S1-J09 | Recorrido Gateway→DB B→RabbitMQ con 202 | Parcial | Gateway 26 checks | E2E completo con pila real |
| S1-J10 | Corrección hallazgos, smoke Sprint 1 | Parcial | Smoke cliente regresión | Smoke completo con pila, Newman |

* `Parcial` = código presente pero con brechas frente al PDF original documentadas en §2–3 arriba.
La dependencia de integración con B/formulario (CA2 US03) quedó registrada para coordinar con Sebastián y Alex; no se implementa por decisión del usuario.

### Próximos pasos para cierre de Sprint 1

1. **SQL Server + RabbitMQ reales:** ejecutar `Siha.Integracion.Tests` (Testcontainers) y smoke E2E con la pila completa levantada. Requiere Docker Desktop Linux.
2. **Migración `AsignaturaActiva`:** aplicar a base de destino antes de usar el modelo.
3. **Coordinación B/formulario:** acordar referencias inequívocas de catálogo (malla+código o IDs) y selección de materias aprobadas en frontend.
4. **Smoke completo + Newman:** contra pila real con datos de QA y parámetros modificados.

No se ejecutó el E2E destructivo, no se borraron volúmenes y no se hizo push.
