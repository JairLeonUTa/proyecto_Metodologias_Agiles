# Catálogo: CRUD y validación compartida

## Contrato

Solo `ADMINISTRADOR` puede escribir; las lecturas conservan autenticación para cualquier rol.

| Operación | Resultado |
|---|---|
| `DELETE /api/carreras/{id}` | `Activa=false` |
| `DELETE /api/mallas/{id}` | `Vigente=false` |
| `DELETE /api/asignaturas/{id}` | `Activa=false` |
| `PUT /api/mallas/{id}` | Edita versión, año, fuente, ruta normativa y vigencia |

Las bajas devuelven **204**, incluso repetidas; un ID inexistente devuelve **404**.
No borran filas, unidades ni referencias, ni cambian en cascada el estado de sus hijos.
Los listados y consultas por ID siguen incluyendo históricos/inactivos con su estado explícito.
La edición de metadatos históricos sigue permitida y no reactiva asignaturas.
`PlanCompleto` expresa integridad del plan, no vigencia de carrera/malla/asignatura.
La selección para procesos futuros debe comprobar ambos conceptos.

El contrato de edición de malla **no contiene `CarreraId`**: no se puede trasladar entre carreras.
Los códigos y versiones se comparan con `Trim` + mayúsculas, incluyendo registros históricos
e inactivos; crear un duplicado devuelve **409**. En importación se rechaza el ítem duplicado.
No se reutilizan identificadores mediante una baja.

## Política común de planes

Alta, edición e importación usan `ValidacionCatalogo`:

- Rechazan créditos fuera de 1–30, horas fuera de 1–2000 y semestre fuera de 1–20.
- Rechazan unidades con número fuera de 1–50, número duplicado, horas fuera de 1–1000,
  nombre vacío/demasiado largo, contenidos demasiado largos o entradas nulas.
- Código y nombre son obligatorios, con los límites existentes del DTO y la base.
- Una petición inválida no modifica la asignatura ni reemplaza sus unidades.
- Un plan válido pero sin unidades, con horas distintas de `48 × créditos` o con suma de
  unidades distinta de las horas declaradas se guarda **pendiente**, con advertencias.
- Los textos de contenidos pueden estar vacíos: no se inventan datos de los planes reales.

El DTO de asignatura añade `advertencias`. La importación mantiene su contrato de
`importadas`, `rechazadas` y `errores` (incluye advertencias); conserva la importación parcial
de ítems válidos. La validación automática HTTP de los DTO puede rechazar el lote completo
con 400 antes de entrar al servicio si hay campos fuera de rango.

Editar sin enviar `unidades` conserva las existentes pero **siempre recalcula `PlanCompleto`**;
enviar `[]` las vacía y deja el plan pendiente. Las lecturas recalculan el valor expuesto y las
advertencias sin actualizar silenciosamente las filas históricas. No se altera el índice único
`(AsignaturaId, Numero)`.

## Verificación y límites

`CatalogoValidacionTests` y `CatalogoCrudTests` son pruebas **simuladas con EF InMemory**;
no demuestran restricciones/transacciones SQL Server ni autorización real por Gateway.
Se conservaron las pruebas `CatalogoImportacionRealTests` con datos de planes reales.
Se ejecutan con `dotnet test tests/Siha.MsA.Tests/Siha.MsA.Tests.csproj`.
En el entorno sin runtime 8, se usó `DOTNET_ROLL_FORWARD=Major` únicamente en el proceso de
verificación (.NET 10). Las corridas se hicieron sobre copia temporal aislada para no compilar
el proyecto mientras otros agentes lo editan.

Resultado de la copia del 20-oct-2026: **91/91 pruebas MsA correctas, 0 omitidas**.
La verificación integrada posterior, incluida la regresión del seeder, está en
`evidencias/correcciones-jair-20261020.md` y su TRX versionado.
TDD: 7 fallos iniciales de validación y 5 fallos de CRUD reproducidos antes de implementar;
una regresión adicional confirmó el rechazo seguro de unidades nulas.
TRX local: `Temp/opencode/catalogo-sprint1-check/results/catalogo-msa-simuladas.trx`.

La migración `AsignaturaActiva` pertenece al bloque de esquema y debe aplicarse antes del
despliegue. No se modificó ni aplicó desde este bloque.
La semilla también usa el evaluador compartido: sus muestras de 192 h con solo 48 h
de unidades quedan pendientes. No se agregaron unidades sintéticas para completarlas.
