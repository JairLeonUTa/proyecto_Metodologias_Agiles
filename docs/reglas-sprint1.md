# Reglas y parámetros · Sprint 1

## Fuente y alcance

Se usa **como fuente secundaria** `docs/evidencias/validacion-funcional-sprint1-paul-acurio.md`,
§5–6 (revisión Paul Acurio, disponible en `origin/develop`). El PDF maestro no está disponible
en este entorno: no se afirma haberlo consultado ni se amplía la cobertura normativa a 19 reglas.
Las cinco semillas representan la cobertura parcial ya existente, con IDs alineados al mapa de esa revisión.
No se necesita migración de esquema para este bloque: se reutilizan filas, índices y fechas existentes.

## Contrato REST de MsA

Todas las rutas requieren autenticación. Solo `ADMINISTRADOR` (valor de `RolesSistema.Administrador`)
puede escribir; se conserva la política de autorización existente. Sin token: 401; otro rol al escribir: 403.

| Método y ruta | Semántica | Respuesta |
|---|---|---|
| `GET /api/reglas` | Solo activas con `vigenteDesde <= hoy UTC <= vigenteHasta` (fin null = abierto) | 200, lista |
| `GET /api/reglas/historial` | Todas las filas: históricas, futuras, vencidas e inactivas | 200, lista |
| `POST /api/reglas` | Crea una identidad nueva `(codigo, rutaNormativa)` | 201, `ReglaDto`; identidad existente: 400 |
| `PUT /api/reglas/{id:int}` | Versiona la última fila de una identidad; no sobrescribe su contenido | 200, nuevo `ReglaDto` con **otro id**; inexistente: 404; inválido: 400 |
| `GET /api/reglas/parametros/{rutaNormativa}` | Configuración heurística con mayor versión numérica | 200; sin configuración: 404 |
| `PUT /api/reglas/parametros/{rutaNormativa}` | Inserta configuración heurística con versión anterior + 1 (primera = 1) | 200; valores/ruta inválidos: 400 |

Las dos listas aceptan los filtros opcionales exactos `rutaNormativa` y `tipoTramite`.
Orden: ruta, código, inicio de vigencia, id. La consulta gRPC existente reutiliza el listado
**vigente**, no el historial. Cambio observable: GET ya no devuelve filas fuera de vigencia;
los consumidores que necesiten auditoría deben usar `/historial`.

### PUT de una regla

Reutiliza **todo** `CrearReglaRequest`, sin PATCH ni campos implícitos:

```json
{
  "codigo": "RN-02",
  "rutaNormativa": "CAU-P-122",
  "tipoTramite": "EXTERNO",
  "descripcion": "Para homologar se exige al menos 80 % de correspondencia en resultados de aprendizaje.",
  "expresion": "ra>=80",
  "version": "2.0",
  "fuente": "Resolución CAU-P-122, Art. 20",
  "esNormativa": true,
  "vigenteDesde": "2026-10-21",
  "vigenteHasta": null
}
```

- El id debe ser el de la última versión **cronológica** (inicio, luego id); esa fila debe tener `activa=true`.
- `codigo`, `rutaNormativa`, `tipoTramite` y `esNormativa` deben coincidir exactamente con la anterior.
  Cambiarlos es un 400, nunca una renumeración o reclasificación silenciosa.
- `version` es una etiqueta opaca, obligatoria, distinta de **todas** las históricas de esa identidad;
  no se interpreta como SemVer ni se incrementa automáticamente.
- Inicio obligatorio, distinto de fecha mínima y estrictamente posterior al inicio anterior;
  fin null o mayor/igual al nuevo inicio. Se admiten fechas retroactivas y futuras explícitas.
- Se cierra la anterior al día previo al nuevo inicio, **solo si su fin era abierto o posterior**.
  No se alarga una vigencia ya cerrada; puede existir una brecha entre versiones.
- Contenido, fuente, versión e id anteriores se conservan. Solo cambia su fin si hace falta cerrarlo.
  `activa` es habilitación de la fila, **no** sinónimo de vigente: las fechas seleccionan la versión.
  Una sucesora futura no oculta la anterior antes de su inicio.
- POST ya no es una vía alternativa para agregar versiones; rechaza la identidad aunque la etiqueta sea nueva.
- Campos requeridos: código/ruta/tipo/versión (máx. 20), descripción (400), fuente (200).
  Expresión opcional (400). Rutas: `CAU-P-011`/`CAU-P-122`; tipos: `INTERNO`/`EXTERNO`.
- Errores del servicio: `{ "mensaje": "..." }`; errores de binding/atributos MVC conservan su formato estándar.
  Se mantiene 400 para duplicados (H-03); no se introduce 409 en este bloque.
- Repetir el mismo PUT devuelve 400, sin insertar otra fila: para continuar hay que usar el nuevo id
  y otra versión/inicio. No se promete replay idempotente de la respuesta.

En SQL Server, POST, PUT de reglas y PUT de parámetros usan transacción **Serializable**;
el cierre y la inserción se guardan juntos. Las escrituras concurrentes no deben confirmar
dos sucesoras de la misma fila; un deadlock/error transitorio puede requerir reconsulta y reintento.
InMemory no valida bloqueos, transacciones, collation ni restricciones SQL.

### Parámetros y normativa separados

Instalaciones nuevas: ambas rutas usan **theta=80, delta=15, tau=75**, escala **0–100**
(tau=75 equivale a 0,75 del modelo, no enviar 0,75 para representar 75 %).
Se mantienen rangos [0,100], valores finitos y delta <= theta.
Cada actualización válida agrega una fila con autor/fecha y nueva versión; una inválida no cambia nada.
GET devuelve la mayor versión; el historial de parámetros queda persistido en `PARAMETRO_EVALUACION`,
sin un endpoint adicional de auditoría en este bloque.

**RN-02 normativa mantiene `ra>=80`** con fuente CAU-P-122 Art. 20; no referencia theta ni tau.
Cambiar parámetros no cambia ninguna regla normativa. Las expresiones son metadatos del contrato actual,
no un nuevo intérprete o motor de evaluación. Un administrador aún puede versionar explícitamente una
regla normativa bajo su responsabilidad; modificar heurísticas no modifica automáticamente su mínimo.

## Semilla nueva y mapa legado

| Código legado | Código alineado | Naturaleza y alcance conservado |
|---|---|---|
| RN-01 | RN-09 | Normativa; materias aprobadas/malla de origen (parcial) |
| RN-02 | RN-13 | Heurística de créditos; `creditosHomologables>=theta`, no mínimo legal de créditos |
| RN-03 | RN-10 | Normativa; documentación de movilidad (parcial) |
| RN-04 | RN-14 | Heurística de cobertura; `cobertura>=tau`, sin confundir tau con banda 65 |
| RN-05 | RN-02 | Normativa RA; `ra>=80`, independiente de theta |

Las fechas iniciales existentes de la semilla (2026-01-01) son configuración de arranque del proyecto,
no certificación de una fecha oficial. RN-14 es representación parcial del modelo, no implementación
completa de ponderación temática ni motor IA. No se agregan filas oficiales no verificadas.

## Instalaciones existentes: actualización explícita

El seeder conserva sus guardas: si ya hay cualquier fila en reglas o parámetros, no repuebla ese
conjunto. **No renumera**, no recalibra, no completa conjuntos parciales ni elimina historial existente.
El mapa anterior permite interpretar las instalaciones legadas sin modificar datos.

1. Respaldar la base y consultar `/historial` para inventariar códigos, fechas, versiones y naturaleza.
2. Para adoptar 80/15/75, un administrador envía explícitamente
   `PUT /api/reglas/parametros/CAU-P-011` y `PUT /api/reglas/parametros/CAU-P-122`
   con `{"theta":80,"delta":15,"tau":75}`. Se conserva la última configuración anterior como otra fila.
   Valores ya perdidos por versiones antiguas del servicio **no se pueden reconstruir**.
3. Para corregir una expresión/descripción legada, usar PUT sobre su último id, **conservando su código**,
   fuente y naturaleza, y elegir nueva etiqueta e inicio de vigencia explícitos.
   Ejemplo: RN-05 legado debe seguir siendo RN-05 al cambiar `theta>=80` a `ra>=80`.
4. No hay renumeración automática por API: colisionar RN-02 legado (heurística) con RN-02 oficial
   (normativa) destruiría significado. Mantener el mapa es la opción soportada. Si se exige renumerar
   una base existente, hace falta un procedimiento de datos revisado que resuelva colisiones y referencias;
   **no ejecutado ni incluido aquí**.
5. Si la base ya contiene versiones solapadas creadas por el POST antiguo, revisar/cerrar sus vigencias
   mediante mantenimiento autorizado antes de adoptar el flujo nuevo. No se corrigen silenciosamente
   inconsistencias históricas ni se selecciona arbitrariamente una de ellas.

## Gateway integrado

Publicado hacia MsA, con autenticación y autorización por rol:

- `PUT /api/reglas/{id}` → misma ruta en MsA; solo Administrador.
- `GET /api/reglas/historial` → misma ruta; cualquier usuario autenticado.
- Conservar las rutas de parámetros `/api/reglas/parametros/{rutaNormativa}` y los alias actuales,
  si existen. La ruta de historial es literal; no debe quedar capturada por una regla genérica de ids.

`/api/reglas/historial` atraviesa la plantilla GET `/api/reglas/{everything}`; el controlador
lo resuelve como ruta literal. La regresión HTTP del Gateway utiliza upstream simulado,
no demuestra la ejecución de estas operaciones en SQL.

## Pruebas simuladas reproducibles

`tests/Siha.MsA.Tests/ReglasServiceTests.cs`: xUnit, servicio/seeder reales sobre EF **InMemory**,
etiqueta `Entorno=Simulada-InMemory`; no equivale a evidencia de pila real, SQL Server o RBAC HTTP.
La prueba de autorización verifica metadatos del controlador, no respuestas HTTP 401/403.

```powershell
$anterior = $env:DOTNET_ROLL_FORWARD
try {
    $env:DOTNET_ROLL_FORWARD = 'Major' # net8.0 con runtime 10 disponible
    dotnet test tests/Siha.MsA.Tests/Siha.MsA.Tests.csproj --no-restore --filter FullyQualifiedName~ReglasServiceTests
    dotnet test tests/Siha.MsA.Tests/Siha.MsA.Tests.csproj --no-restore
} finally {
    $env:DOTNET_ROLL_FORWARD = $anterior
}
```

Red observado antes del cambio: tres fallos de comportamiento (POST acepta nueva versión,
parámetros conserva una sola fila, semillas RN-01…RN-05), más ausencia de métodos de versionado/historial.
La prueba de no sobrescribir configuraciones ya pasaba. Tras implementar se verifican también
vigencia futura/actual, histórico, identidad, etiquetas/fechas inválidas y contrato 200/400/404 del controlador.

Las 10 pruebas originales del bloque de reglas pasaron. La verificación integrada posterior
incluye otra regresión del seeder; resultados y TRX versionados en
`evidencias/correcciones-jair-20261020.md`. Los fallos transitorios de CRUD durante edición
paralela no se presentan como resultado final.
