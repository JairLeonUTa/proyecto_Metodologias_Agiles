# Correcciones de Jair · Sprint 1 · 20-oct-2026

Rama: `develop`, después de integrar `main` conservando las credenciales externalizadas.
Fuente funcional/normativa **secundaria**: revisión de Paul en
`validacion-funcional-sprint1-paul-acurio.md`, aprobada por el usuario como referencia.
No se consultó el PDF maestro ni se certifica cumplimiento de las 19 RN.

## Resultados ejecutados en esta sesión

| Comprobación | Resultado observado | Alcance y artefacto |
|---|---|---|
| `dotnet build SIHA-IA.sln --no-restore --nologo` | 0 errores, 0 advertencias | Incluye compilación de integración, no su ejecución. `build-correcciones-jair-20261020.txt` |
| MsA xUnit | **92/92**, 0 omitidas | EF InMemory, servicio/controladores, semillas, modelo/migración. `msa-correcciones-jair-20261020.trx` |
| MsB xUnit | **66/66**, 0 omitidas | Mocks/InMemory/arnés MassTransit y comprobaciones locales de archivos/configuración. `msb-correcciones-jair-20261020.trx` |
| Gateway Ocelot HTTP real | **26 comprobaciones**, salida 0 | Backend HTTP **simulado**, puertos y configuración aislados. `gateway-correcciones-jair-20261020.txt` |
| Regresión del cliente smoke PS7 | Salida 0 | Servidor HTTP local real devuelve 400; conserva cuerpo UTF-8 y rechaza JSON inválido/cuerpo incorrecto con 200. `smoke-cliente-correcciones-jair-20261020.txt` |
| Gateway HTTP real (502→503) | 26 comprobaciones, salida 0 | Backend HTTP simulado, mapeo personalizado ConnectionToDownstreamServiceError→503. `gateway-correcciones-jair-20261020.txt` |
| Script incremental EF | Generado, no ejecutado en SQL | `asignatura-activa-correcciones-jair-20261020.sql`: añade columna no nula con default true |

Entorno: Windows, SDK **10.0.401**, runtime disponible **10.0.12**.
La solución sigue dirigida a `net8.0`; falta su runtime nativo. Las ejecuciones usaron
`DOTNET_ROLL_FORWARD=Major` **solo durante el proceso**, restaurando su valor al terminar.
No equivalen a una ejecución nativa .NET 8. PowerShell **7.5.4** se obtuvo como herramienta
local temporal; no se agregó dependencia a la aplicación ni se instaló globalmente.

Las líneas `[FALLO]` de los dos ejemplos negativos del cliente smoke son **esperadas**:
la regresión comprueba que el verificador las clasifica como fallo y termina con salida 0.
Ese artefacto no es una corrida del smoke completo contra la pila SIHA.

## Reproducciones previas al arreglo

- PowerShell 7: `HttpResponseMessage` no contiene `GetResponseStream`; un HTTP 400
  rompía el manejo de errores. Ahora `SkipHttpErrorCheck` conserva estado y contenido.
- Gateway HTTP aislado: OPERADOR obtuvo 200 en DELETE de carrera, cuando debía recibir 403.
  El URI de rol usado como clave de `RouteClaimsRequirement` contiene `:`, separador de
  IConfiguration. Ahora se comprueba `rolGateway`, derivado únicamente después de validar
  el JWT. La regresión incluye anónimo, OPERADOR, DOCENTE y ADMINISTRADOR, lecturas de
  DOCENTE y rechazo de alias administrativo sin rol correspondiente.
- Configuración Gateway: faltaban métodos PUT/DELETE; dos casos de configuración fallaron
  antes de añadir rutas y pasaron después.
- Semilla: tres asignaturas con 192 h pero solo 48 h de unidades tenían
  `PlanCompleto=true`; la regresión falló y pasó tras usar el evaluador compartido.
- Documentos: con dos adjuntos y uno alterado conservando tamaño se publicaba el
  subconjunto restante. Ahora responde 422, bloquea reintentos y exige reparar la carga.
- Catálogo/reglas: se reprodujeron fallos de CRUD, normalización, recálculo,
  historial de parámetros y semillas antes de aplicar las correcciones.

## Comandos reproducibles

Con .NET 8 instalado no se necesita roll-forward. En el entorno de esta sesión:

```powershell
$previo = $env:DOTNET_ROLL_FORWARD
try {
    $env:DOTNET_ROLL_FORWARD = 'Major'
    dotnet build SIHA-IA.sln --no-restore --nologo
    dotnet test tests/Siha.MsA.Tests/Siha.MsA.Tests.csproj --no-build --logger "trx;LogFileName=msa-correcciones-jair-20261020.trx" --results-directory docs/evidencias
    dotnet test tests/Siha.MsB.Tests/Siha.MsB.Tests.csproj --no-build --logger "trx;LogFileName=msb-correcciones-jair-20261020.trx" --results-directory docs/evidencias
    node scripts/test-gateway-rutas.cjs
    pwsh -NoProfile -File scripts/test-smoke-sprint1.ps1
} finally { $env:DOTNET_ROLL_FORWARD = $previo }
```

El Gateway de prueba carga una copia de las rutas y reemplaza los upstreams por un
servidor de prueba local. No llama a los microservicios existentes ni envía DELETE a sus datos.
Las claves/tokens de prueba son efímeros y no se imprimen ni versionan.

El script SQL se generó con `dotnet ef migrations script 20261018145128_InitialCreate
20261020190757_AsignaturaActiva --project src/Services/Siha.MsA.Api --no-build --output
docs/evidencias/asignatura-activa-correcciones-jair-20261020.sql`, usando una conexión
ficticia temporal, sin abrirla. EF advirtió que no pudo inicializar el host por falta
de JWT y continuó con la fábrica de diseño existente; produjo el script de la migración.
No se ejecutó `database update` ni se usaron credenciales de servicios existentes.

## Resultados finales con .NET 8 nativo

| Comprobación | Resultado observado | Alcance y artefacto |
|---|---|---|
| `dotnet build SIHA-IA.sln --no-restore --nologo` | 0 errores, 0 advertencias | Incluye integración y Gateway. `build-correcciones-jair-20261020.txt` |
| MsA xUnit | **113/113**, 0 omitidas | EF InMemory, controladores/servicios/hasher/JWT, semillas, modelo/migración. `msa-final-20261020.trx` |
| MsB xUnit | **66/66**, 0 omitidas | Mocks/InMemory/arnés MassTransit y comprobaciones locales de archivos/configuración. `msb-final-20261020.trx` |
| Gateway Ocelot HTTP real | **26 comprobaciones**, salida 0 | Backend HTTP simulado, JWT firmado, alias `rolGateway`, mapeo 503. `gateway-correcciones-jair-20261020.txt` |
| Cliente smoke PS7 | No ejecutado (pwsh 7 no instalado global) | Regresión local HTTP 400/validación JSON verificada en sesión anterior. `smoke-cliente-correcciones-jair-20261020.txt` |

Entorno: Windows, SDK **8.0.426** local, runtime **8.0.32**.
Las ejecuciones **no requieren** `DOTNET_ROLL_FORWARD=Major` tras instalar el SDK 8 nativo.

## Pendiente / no verificado

- **SQL Server y RabbitMQ reales:** no ejecutados. `docker version` falló por ausencia
  de `dockerDesktopLinuxEngine`. InMemory no acredita índices/collation, orden de
  reemplazo de unidades, transacciones ni concurrencia relacional.
- **Migración `20261020190757_AsignaturaActiva`:** generada y comprobada en modelo/script;
  **no aplicada a una base existente**. Debe desplegarse antes de usar el nuevo modelo.
- **Evento recibido por un consumidor real y PDF en volumen desplegado:** no verificados
  en esta sesión. El test real se amplió para hash alterado/referencias y compila, pero no
  se ejecutó. Un 202 o `publicado=true` no sustituyen esa evidencia.
- **Smoke completo y Newman:** no ejecutados contra la pila. La colección solo se
  validó sintácticamente; sus evidencias históricas no son resultados de esta corrección.
- **Coordinación con Sebastián y Alex:** contrato/guía actualizados, **sin contacto ni
  aceptación externa**. Falta acordar cómo C accede autorizadamente al documento y su
  deduplicación. No se certifica recepción/procesamiento por C.
- **Instalaciones existentes:** no se renumeraron reglas ni se cambiaron heurísticas
  automáticamente; usar mapa legado y actualización administrativa de `../reglas-sprint1.md`.

No se ejecutó el E2E destructivo, no se borraron volúmenes y no se hizo push.
No se marca el conjunto S1-J01…S1-J10 como completamente verificado.
