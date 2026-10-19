# Rolando Sebastián Miranda Baño — Sprint 1

**Rol:** Backend / QA & DevOps
**Tareas asignadas:** S1-S01 a S1-S10 (10 tareas)
**Carpeta:** `entregas-por-integrante/3-Miranda-Bano-Rolando-Sebastian`

## Tareas y estado

| Tarea | Descripción | Estado | Evidencia |
|-------|-------------|--------|-----------|
| S1-S01 | Contenedores Docker Compose para bases y servicios | Verificada | `docker-compose.yml` y `docker-compose.override.yml`. `docker compose config` válido y siete contenedores en estado healthy (DB C bajo perfil `sprint23`). |
| S1-S02 | Autenticación JWT con claims de rol y expiración | Verificada | `Security/JwtTokenService.cs` (Microservicio A). Token con claim de rol y expiración; login 200 y rechazo de credenciales inválidas 401. |
| S1-S03 | Pruebas unitarias xUnit y esquema de DB B | Verificada | `tests/Siha.MsA.Tests` (catálogo, parámetros, seguridad) y `tests/Siha.MsB.Tests` con la migración inicial de DB B. Suite completa: 102 pruebas, 0 fallos. |
| S1-S04 | Servidor gRPC de reglas en el Microservicio A | Verificada | `Protos/reglas.proto`, `GrpcServices/ReglasGrpcService.cs`. Listener HTTP/2 (h2c) dedicado, expuesto en `localhost:5083` y reportado por `/health`. |
| S1-S05 | Alta de solicitud interna/externa y validación de PDF | **Parcial** | `Controllers/DocumentosController.cs`, `Services/ArchivoStorageService.cs`. Validación de firma, tamaño y MIME con 49 pruebas unitarias; no se ejecutó una subida multipart real. |
| S1-S06 | Almacenamiento seguro de PDF y metadatos en DB B | Verificada | `ArchivoStorageService` (nombre GUID, subcarpeta AAAA/MM, verificación anti path traversal, hash SHA-256) y migración `AgregarDocumentosYAprobacionCertificada`. |
| S1-S07 | Pruebas de integración del Gateway y del bus | Verificada | `tests/Siha.MsB.Tests/GatewayOcelotConfiguracionTests.cs` y `BusConectividadTests.cs`, incluidas en la suite de 49 pruebas. |
| S1-S08 | Pipeline de integración continua | **Entregada** | `.github/workflows/ci.yml` (restore, build y test en Release con artefacto TRX). No se ha ejecutado porque el proyecto no cuenta con repositorio remoto. |
| S1-S09 | Pruebas integradas de reglas, expedientes, PDF y evento | **Parcial** | `ExpedientesControllerTests.cs` y `SolicitudCreadaEventContractTests.cs`. Cubre el contrato del evento y el flujo del controlador; la parte de PDF queda pendiente de un archivo real. |
| S1-S10 | Arranque limpio en Compose y guía de ejecución | Verificada | `docker-compose.yml` y `README.md`. Arranque limpio del stack completo verificado tras reconstruir las imágenes. |

## Qué contiene esta carpeta

```
src/Services/Siha.MsA.Api/Protos/reglas.proto               Contrato gRPC de reglas (S1-S04)
src/Services/Siha.MsA.Api/GrpcServices/ReglasGrpcService.cs  Servidor gRPC (S1-S04)
src/Services/Siha.MsB.Api/Controllers/DocumentosController.cs    Carga y consulta de PDF (S1-S05)
src/Services/Siha.MsB.Api/Services/ArchivoStorageService.cs      Validación y almacenamiento (S1-S05/S06)
src/Services/Siha.MsB.Api/Contracts/Dtos/DocumentoDtos.cs
src/Services/Siha.MsB.Api/Domain/Entities/ExpedienteDocumento.cs
src/Services/Siha.MsB.Api/Domain/EstadosRecepcionDocumento.cs
src/Services/Siha.MsB.Api/Options/ArchivosOptions.cs
src/Services/Siha.MsB.Api/Migrations/20261018151155_AgregarDocumentosYAprobacionCertificada.*
tests/Siha.MsB.Tests/            ocho archivos de prueba + utilidades (S1-S03, S1-S07, S1-S09)
tests/Siha.MsA.Tests/            CatalogoServiceTests, ParametrosValidatorTests, SeguridadTests
                                 y el .csproj de la suite (S1-S03). RbacTests.cs es de Paul.
docker-compose.yml, docker-compose.override.yml               Stack completo (S1-S01, S1-S10)
.github/workflows/ci.yml                                      Pipeline de CI (S1-S08)
```

## Archivos compartidos con otros integrantes

- `src/Services/Siha.MsB.Api/Program.cs` (carpeta de Jair) es quien **registra** este módulo de
  archivos: los servicios, las opciones y el `ArchivoStorageService` se inyectan allí.
- `src/Services/Siha.MsB.Api/Data/SihaDbContextB.cs` (carpeta de Jair) declara el `DbSet` de
  `ExpedienteDocumento` que usa este módulo.
- `src/Services/Siha.MsA.Api/Program.cs` (carpeta de Jair) es donde se levanta el listener gRPC
  y donde entra el `.proto` de esta carpeta.
- Las pruebas de `tests/Siha.MsA.Tests` comparten el `.csproj` con `RbacTests.cs`, que es de Paul.

## Cómo ejecutar mis pruebas

Desde la raíz del repositorio:

```bash
dotnet test SIHA-IA.sln -c Release
```

## Cómo subir mi parte

```bash
git pull
git add entregas-por-integrante/3-Miranda-Bano-Rolando-Sebastian
git commit -m "Sprint 1 - Entrega de Sebastian Miranda (S1-S01 a S1-S10)"
git push
```
