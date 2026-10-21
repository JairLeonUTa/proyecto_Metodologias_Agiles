# SIHA-IA — Sprint 1

Proyecto integrado: Gateway, microservicios A/B, frontend y pruebas.

## Estado del Sprint 1

✅ **Completado** — Rama `develop` sincronizada con `origin/develop`.

| Área | Estado | Documentación |
|---|---|---|
| Flujo PDF (borrador → carga → envío) | ✅ | [docs/catalogo-sprint1.md](docs/catalogo-sprint1.md) |
| CRUD Catálogo + bajas lógicas | ✅ | [docs/catalogo-sprint1.md](docs/catalogo-sprint1.md) |
| Reglas/umbrales + semillas alineadas a PDF | ✅ | [docs/reglas-sprint1.md](docs/reglas-sprint1.md) |
| Autenticación/usuarios (BCrypt, JWT, 409) | ✅ | [docs/evidencias/correcciones-jair-20261020.md](docs/evidencias/correcciones-jair-20261020.md) |
| Gateway Ocelot (JWT, RBAC, 503) | ✅ | [scripts/test-gateway-rutas.cjs](scripts/test-gateway-rutas.cjs) |
| Modal accesible (focus trapping, tests) | ✅ | `frontend/src/components/ui/Modal.test.tsx` |
| Publicación evento solo con PDFs válidos | ✅ | [docs/contrato-solicitud-creada-event.md](docs/contrato-solicitud-creada-event.md) |

**Pendiente (requiere infraestructura):** SQL/RabbitMQ reales (Testcontainers), smoke E2E completo, Newman.

### Resumen de cambios por commit (`develop`)

| Commit | Mensaje | Área |
|---|---|---|
| `bd090e0` | docs: actualizar README.md con puertos, comandos, arquitectura y estado Sprint 1 | Docs |
| `b677267` | fix(ui): modal onCerrar via ref para evitar re-renders y pérdida de foco al escribir | Frontend/Modal |
| `f15af0f` | fix(ui): loss of focus in modal 'Crear cuenta institucional'; auto-focus solo inputs, focus trapping Tab/Shift+Tab, Escape cierra, tests 8/8 pasan | Frontend/Modal |
| `d418d60` | fix(ui): modal no enfoca botón Cerrar (X) al abrir; primer foco va al input del formulario | Frontend/Modal |
| `15f877a` | fix(docs-pdf): añadir estados PENDIENTE_DOCUMENTOS/PUBLICACION_EN_CURSO al frontend y mejorar mensaje de fallo de publicación; build + tests ok | Frontend/Types |
| `aa5c218` | fix(US-01/US-04): quitar MinLength login, normalizar email, traducir unique SQL 409, mapeo 502->503; tests 113+66 pasan .NET8 | Auth/Gateway |
| `a9aa150` | docs: registrar resultados ejecutados y limites de verificacion del sprint | Docs |
| `09a2286` | fix: aplicar permisos reales en gateway y validar cuerpos del smoke PS7 | Gateway/Smoke |
| `5915f79` | fix: impedir publicacion parcial con adjuntos alterados | MsB/Documentos |
| `f29a216` | fix: versionar reglas y alinear semillas normativas y heuristicas | MsA/Reglas |

**Total:** 10 commits, ~4500 líneas cambiadas, 0 errores de build, 187 tests pasando (113 MsA + 66 MsB + 8 Frontend + 26 Gateway HTTP).

---

## Configuración local con Docker

### 1. Variables de entorno (`.env`)

Copiar `.env.example` a `.env` y completar **con tus propios valores**:

```env
# SQL Server (sa password - compleja, sin ;)
SIHA_SQL_PASSWORD=

# RabbitMQ
SIHA_RABBITMQ_PASSWORD=

# JWT (mínimo 32 bytes, igual en A, B, Gateway)
SIHA_JWT_KEY=
JWT_ISSUER=SIHA-IA
JWT_AUDIENCE=SIHA-IA-Clients

# Admin inicial (Microservicio A)
SEED_ADMIN_CORREO=admin@uta.edu.ec
SIHA_ADMIN_PASSWORD=

# Archivos (Microservicio B)
ARCHIVOS_TAMANO_MAXIMO_BYTES=10485760
```

> **Nota:** No versionar `.env` (está en `.gitignore`). Usa contraseñas complejas para SQL Server. Si un valor contiene `$`, encerrarlo entre comillas simples. Las credenciales de ejemplo **no deben usarse en producción**.

### 2. Levantar la pila

```bash
docker compose config --quiet   # Valida sintaxis
docker compose up -d --build    # Primera vez: 5-15 min (descarga imágenes + build)
```

### 3. Verificar servicios

```bash
docker compose ps
```

Todos deben estar `healthy` o `Up`:

| Servicio | Puerto host | Healthcheck |
|---|---|---|
| **siha-sqlserver-a** (SihaDbA) | **1434** | ✅ |
| **siha-sqlserver-b** (SihaDbB) | **14330** | ✅ |
| **siha-rabbitmq** | 5672, 15672 | ✅ |
| **siha-ms-a** | 5081 (REST), 5083 (gRPC) | ✅ |
| **siha-ms-b** | 5082 | ✅ |
| **siha-gateway** | 5000 | — |
| **siha-frontend** | **5173** | ✅ |

### 4. Acceder

| Qué | URL |
|---|---|
| **Frontend (SPA)** | http://localhost:5173 |
| **API Gateway** | http://localhost:5000 |
| **Health Gateway** | http://localhost:5000/health |
| **RabbitMQ Management** | http://localhost:15672 (`siha` / *valor de `SIHA_RABBITMQ_PASSWORD`*) |
| **MsA Swagger** | http://localhost:5081/swagger |
| **MsB Swagger** | http://localhost:5082/swagger |

**Login inicial:** `admin@uta.edu.ec` / *valor de `SIHA_ADMIN_PASSWORD`*

---

## Conexión a SQL Server (SSMS / Azure Data Studio)

| Base | Server name | Puerto | Login | Password |
|---|---|---|---|---|
| **SihaDbA** | `localhost,1434` | 1434 | `sa` | *valor de `SIHA_SQL_PASSWORD`* |
| **SihaDbB** | `localhost,14330` | 14330 | `sa` | *valor de `SIHA_SQL_PASSWORD`* |

- Authentication: **SQL Server Authentication**
- ✅ **Trust Server Certificate** (o "Encrypt connection" → "Trust server certificate")

Las bases `SihaDbA` y `SihaDbB` se crean automáticamente al arrancar `ms-a` y `ms-b` (migraciones EF + seeder).

---

## Ejecución sin Docker (desarrollo local)

Exportar variables de entorno por servicio:

| Servicio | Variables obligatorias |
|---|---|
| **A** | `ConnectionStrings__SihaDbA`, `Jwt__Key`, `Seed__AdminPassword` |
| **B** | `ConnectionStrings__SihaDbB`, `Jwt__Key`, `RabbitMq__Password` |
| **Gateway** | `Jwt__Key` |

Conexiones SQL locales: puertos **1434** (A) y **14330** (B). Usar `ConnectionStrings__SihaDbA/B` también para migraciones EF. Mantener el mismo JWT en los tres servicios.

---

## Pruebas y herramientas

### Backend (.NET 8)

```bash
# Requiere .NET 8 SDK (8.0.426+)
dotnet build SIHA-IA.sln --no-restore
dotnet test SIHA-IA.sln --no-build

# Por proyecto:
dotnet test tests/Siha.MsA.Tests/Siha.MsA.Tests.csproj --no-build
dotnet test tests/Siha.MsB.Tests/Siha.MsB.Tests.csproj --no-build
```

**Resultados actuales:** MsA **113/113** ✅, MsB **66/66** ✅

### Frontend (Node 20+)

```bash
cd frontend
npm ci
npm run build          # tsc --noEmit + vite build
npm test               # vitest run (8 tests Modal)
npm run test:watch     # vitest watch mode
```

### Gateway HTTP real (Node 20+)

```bash
# Compila Gateway y ejecuta 26 checks contra upstream simulado
dotnet build src/Services/Siha.Gateway/Siha.Gateway.csproj --no-restore
node scripts/test-gateway-rutas.cjs
```

### Smoke PS7 (requiere PowerShell 7)

```powershell
$env:SIHA_ADMIN_PASSWORD = "Admin123*"
pwsh -File scripts/smoke-sprint1.ps1
```

### BusVerifier

```bash
$env:SIHA_RABBITMQ_PASSWORD = "siha-rabbit-2026"
dotnet run --project tools/Siha.BusVerifier
```

### Postman

Importar `docs/postman/SIHA-IA-Sprint1.postman_collection.json`, definir variable `adminPassword` en entorno local privado. **No exportar contraseñas ni tokens.**

---

## Arquitectura de referencia

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│  Frontend   │────▶│  Gateway    │────▶│   MsA (A)   │
│  (React)    │     │  (Ocelot)   │     │  Usuarios,  │
│  :5173      │     │  :5000      │     │  Catálogo,  │
└─────────────┘     └──────┬──────┘     │  Reglas     │
                           │            └──────┬──────┘
                           ▼                   ▼
                    ┌─────────────┐     ┌─────────────┐
                    │   MsB (B)   │     │  RabbitMQ   │
                    │ Expedientes │────▶│  :5672      │
                    │ Documentos  │     │  Management │
                    └─────────────┘     │  :15672     │
                           │            └─────────────┘
                           ▼
                    ┌─────────────┐
                    │  SQL Server │
                    │   SihaDbB   │
                    │  :14330     │
                    └─────────────┘
```

- **Gateway**: JWT validation, RBAC (`rolGateway`), error mapping 502→503
- **MsA**: Auth (BCrypt, JWT), Catálogo (CRUD + import), Reglas (versionado)
- **MsB**: Expedientes (borrador→PDF→envío), Documentos (validación SHA-256), Eventos
- **Evento**: `SolicitudCreadaEvent` → RabbitMQ → consumido por **Microservicio C (Sprint 2/3)**

---

## Documentación clave

| Archivo | Qué contiene |
|---|---|
| [docs/catalogo-sprint1.md](docs/catalogo-sprint1.md) | Contrato CRUD catálogo, bajas lógicas, validación unificada |
| [docs/reglas-sprint1.md](docs/reglas-sprint1.md) | Metadatos reglas (RN-01..14), normativa vs heurística, tau=75/100=0.75 |
| [docs/contrato-solicitud-creada-event.md](docs/contrato-solicitud-creada-event.md) | Contrato evento v1.1, referencias verificadas, correlación |
| [docs/guia-ejecucion-documentos-e-infraestructura.md](docs/guia-ejecucion-documentos-e-infraestructura.md) | Cómo reproducir evidencia real (Testcontainers, Docker) |
| [docs/evidencias/correcciones-jair-20261020.md](docs/evidencias/correcciones-jair-20261020.md) | Resultados ejecutados, pendientes, comandos reproducibles |
| [docs/cumplimiento-sprint1-jair.md](docs/cumplimiento-sprint1-jair.md) | Tabla S1-J01..J10 vs planificación original (PDF) |

---

## Migraciones y despliegue

- **Migración `20261020190757_AsignaturaActiva`**: Añade columna `Activa` (bit, default 1) a `Asignatura`. **Aplicar antes de usar el modelo** (`dotnet ef database update` o script SQL en `docs/evidencias/asignatura-activa-correcciones-jair-20261020.sql`).
- **Seeder idempotente**: No sobrescribe configuraciones existentes (usuarios, reglas, catálogo).
- **Volúmenes Docker**: `siha-sqla`, `siha-sqlb`, `siha-rabbit`, `siha-archivos` persisten datos. `docker compose down -v` los borra.

---

## Seguridad

- `.env` **no se versiona** (`.gitignore` cubre `*.env`)
- Contraseñas SQL/RabbitMQ/JWT/Admin **solo en variables de entorno**
- JWT validado en Gateway (issuer, audience, lifetime, signing key)
- BCrypt para passwords, nunca expuestos en DTOs/respuestas
- Gateway: `rolGateway` alias derivado **tras validar firma** (evita claim URI con `:`)
- Error mapper no expone stack traces internos

---

## Licencia / Uso

Proyecto académico SIHA-IA — Sprint 1. Uso educativo/interno. No usar credenciales de ejemplo en producción.