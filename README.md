# SIHA-IA — Sprint 1

Proyecto integrado: Gateway, microservicios A/B, frontend y pruebas.

## Configuración local con Docker

1. Copiar `.env.example` a `.env` en la raíz.
2. Completar las cuatro variables con valores propios. La contraseña SQL debe cumplir la complejidad de SQL Server y no contener `;`. Usar una clave JWT aleatoria de al menos 32 bytes. Si un valor contiene `$`, encerrarlo entre comillas simples en `.env` para evitar interpolación de Compose.
3. Ejecutar `docker compose config --quiet` y `docker compose up -d --build`.
4. Abrir http://localhost:5173. Usuario inicial: `admin@uta.edu.ec`; contraseña: el valor local de `SIHA_ADMIN_PASSWORD`.

No compartir `.env`, tokens, documentos de estudiantes ni respaldos. Compose falla antes de arrancar si falta alguna variable. Las contraseñas antiguas publicadas deben considerarse expuestas: cambiarlas donde se hayan utilizado. Este commit no borra el historial Git ni cambia credenciales en servicios existentes.

Los volúmenes existentes conservan sus usuarios y contraseñas. Cambiar `.env` no modifica automáticamente las cuentas de SQL Server, RabbitMQ ni el administrador ya creado. Coordinar su cambio en el servicio correspondiente; no borrar volúmenes con datos para aplicar esta configuración.

## Ejecución sin Docker

`.env` es leído por Compose, no por `dotnet run`. Exportar las variables de entorno de cada servicio:

| Servicio | Variables obligatorias |
|---|---|
| A | `ConnectionStrings__SihaDbA`, `Jwt__Key`, `Seed__AdminPassword` |
| B | `ConnectionStrings__SihaDbB`, `Jwt__Key`, `RabbitMq__Password` |
| Gateway | `Jwt__Key` |

Las conexiones SQL locales deben apuntar a los puertos 1433 (A) y 14330 (B). A y B usan `ConnectionStrings__SihaDbA/B` también al ejecutar migraciones EF. Mantener el mismo JWT en los tres servicios.

## Pruebas y herramientas

- Backend: `dotnet test SIHA-IA.sln` con .NET 8.
- Frontend: `npm ci` y `npm run build` dentro de `frontend/`.
- Smoke: establecer `SIHA_ADMIN_PASSWORD` en la sesión y ejecutar `pwsh -File scripts/smoke-sprint1.ps1`.
- BusVerifier: establecer `SIHA_RABBITMQ_PASSWORD` antes de ejecutar la herramienta. Consultar su README para otras opciones.
- Postman: definir `adminPassword` en un entorno local privado. No exportar contraseñas ni tokens. Los valores `<SIHA_...>` en la documentación son marcadores, no contraseñas.

Se versionan migraciones, contratos, pruebas, Compose, Dockerfiles y `frontend/package-lock.json`. Se excluyen dependencias instaladas, resultados de compilación y configuración privada.

## Correcciones S1-J01 a S1-J10 en develop

Contratos: [catálogo y bajas lógicas](docs/catalogo-sprint1.md), [reglas y escala de tau](docs/reglas-sprint1.md).
[Resultados ejecutados y pendientes](docs/evidencias/correcciones-jair-20261020.md): se distinguen pruebas simuladas, Gateway HTTP con upstream simulado y comprobaciones reales pendientes.
La migración `20261020190757_AsignaturaActiva` debe desplegarse antes de usar el nuevo modelo; no se aplicó sobre bases existentes.
Checks aislados: `pwsh -File scripts/test-smoke-sprint1.ps1` y, después de compilar el Gateway, `node scripts/test-gateway-rutas.cjs` (Node 20+).
Las pruebas de `Siha.Integracion.Tests` requieren Docker Linux disponible; el smoke completo requiere la pila ya iniciada y crea datos de QA/modifica parámetros.
