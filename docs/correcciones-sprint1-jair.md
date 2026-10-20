# Correcciones S1-J01 a S1-J10

## Alcance acordado

Trabajo en `develop`, conservando la integración y las credenciales externalizadas.
El usuario aprobó baja lógica de asignaturas con migración incremental, sin eliminar
registros ni reutilizar sus códigos. La referencia normativa disponible es secundaria:
`evidencias/validacion-funcional-sprint1-paul-acurio.md`; no se contrastó el PDF original.

| Bloque | Responsabilidad | Dependencia |
|---|---|---|
| catalogo | CRUD, bajas lógicas, evaluación común de planes, Gateway | modelo y migración incremental |
| reglas | actualización versionada, semillas y correspondencia RN | revisión secundaria de Paul |
| verificacion | contrato documental, smoke PS7 y evidencias | catálogo y reglas |

Orden: catálogo y reglas en paralelo; después verificación conjunta.

## Criterios de aceptación

- DELETE administrativo de carreras, mallas y asignaturas conserva filas y referencias.
  PUT de malla no cambia su carrera. Los códigos dados de baja siguen reservados.
- Crear, editar e importar asignaturas comparten normalización y validación. No se
  persisten números de unidad duplicados. `PlanCompleto` requiere consistencia de
  créditos y horas (48 h/crédito) y suma de horas de unidades igual al total.
  Un plan incompleto puede quedar pendiente con advertencias; no se inventan datos.
- Recalcular al cambiar créditos, horas o unidades, sin exigir reenviar unidades.
- La modificación de reglas conserva versiones previas y documenta vigencia,
  fuentes y separación normativa/heurística. Nuevas instalaciones: 80/15/75;
  tau usa porcentaje [0,100], por tanto 75 equivale a 0,75 del modelo.
  No sobrescribir automáticamente configuraciones administradas existentes.
- El evento no transporta referencias ficticias ni un subconjunto silencioso de
  adjuntos previamente validados que ahora faltan o están alterados. Un 202 no
  acredita recepción por C; coordinación con Sebastián y Alex pendiente de aceptación.
- Smoke requiere PowerShell 7, preserva cuerpos de errores HTTP y comprueba JSON,
  valores, versiones y ausencia de mutaciones tras rechazos; no confunde mocks con E2E.

## Implementación y comprobaciones

Se mantienen controllers/DTOs/services EF existentes, autorización `ADMINISTRADOR`
en escrituras y consultas autenticadas. Sin dependencias nuevas de la aplicación.
Las pruebas siguen xUnit en `tests/Siha.MsA.Tests` y `tests/Siha.MsB.Tests`;
SQL/RabbitMQ reales pertenecen a `tests/Siha.Integracion.Tests` (Testcontainers).

```powershell
dotnet build SIHA-IA.sln --no-restore
dotnet test tests/Siha.MsA.Tests/Siha.MsA.Tests.csproj
dotnet test tests/Siha.MsB.Tests/Siha.MsB.Tests.csproj
dotnet test tests/Siha.Integracion.Tests/Siha.Integracion.Tests.csproj
pwsh -File scripts/test-smoke-sprint1.ps1
node scripts/test-gateway-rutas.cjs
pwsh -File scripts/smoke-sprint1.ps1
```

Nunca borrar volúmenes existentes ni ejecutar el E2E destructivo sobre datos de usuario.
Las migraciones contra bases existentes y el contacto externo requieren autorización.
Registrar en evidencias comando, entorno, resultado y limitaciones: inspección no es ejecución.

### Fuentes técnicas

- PS7 `SkipHttpErrorCheck` conserva respuestas 4xx/5xx:
  https://learn.microsoft.com/powershell/module/microsoft.powershell.utility/invoke-webrequest#-skiphttperrorcheck
- Ocelot 23.4 aplica `RouteClaimsRequirement` después de autenticar:
  https://ocelot.readthedocs.io/en/23.4/features/authorization.html
- Las claves de IConfiguration usan `:` como separador jerárquico:
  https://learn.microsoft.com/aspnet/core/fundamentals/configuration/?view=aspnetcore-8.0#configuration-keys-and-values
