# SIHA-IA (Homologa UTA) — guía rápida

Responder **en español sencillo**. Contexto completo e historial: `CONTEXTO-PARA-CLAUDE.md` (leer solo si hace falta).

## Proyecto
Homologación de materias FISEI-UTA. Interna (cambio de carrera, CAU-P-011) y externa (movilidad, CAU-P-122, con sílabo
de la otra universidad). .NET 8: MS A (usuarios, reglas, catálogo), MS B (expedientes, lectura del PDF), MS C (agente con
embeddings de Ollama), gateway Ocelot, SQL Server, RabbitMQ. Frontend React + Vite + Tailwind en `frontend/`.

## Reglas de negocio
- **Créditos < 80 % bloquean**: "No se puede homologar", `NO_HOMOLOGADA` sin votación (`bloqueadaPorCreditos`).
  **Contenidos < 80 % solo aconsejan**: `NO_CUMPLE` = el agente recomienda no homologar, pero los 2 docentes votan y deciden
  (ej.: el profesor dio temas que no están en el sílabo). `CUMPLE` (ambos ≥ 80 %) → votan los 2 docentes.
  `CRITERIO_DOCENTE` → sin sílabo, deciden docentes. Si los 2 docentes discrepan, decide el Administrador (con comentario).
- El mínimo no se puede bajar de 80. Profundidad y carga horaria son solo dato.
- El estudiante no usa el sistema. Roles: Administrador, Operador, Docente.
- Interfaz sin símbolos (θ, δ, τ) ni errores técnicos. Ver skills `siha-ui` y `frontend-design`.

## Reglas del equipo
- Migraciones EF siempre incrementales. Toda regla nueva con prueba. No subir `.env`.
- Se sube directo a `develop` (sin PR), solo cuando el usuario lo pida, tras `git fetch`.
- Commits con `git commit -F archivo`, **sin** línea `Co-Authored-By` (decisión de Jonathan, 22/11/2026).

## Comandos
- Levantar: `docker compose --profile sprint23 up -d --build` → app http://localhost:5173, gateway http://localhost:5000.
- Frontend: `cd frontend; npm run lint; npm test`.
- Backend (no hay .NET 8 local): `$env:DOTNET_ROLL_FORWARD='LatestMajor'; dotnet test tests\Siha.MsA.Tests` (MsB, MsC igual).
- Integración: solo con .NET 8 en Docker, ver `scripts/pruebas-dotnet8-docker.sh` (~10 min).

## Flujo de trabajo (ahorro de tokens)
Claude = arquitecto, diseñador UI/UX y revisor. Codex (`mcp__codex__codex`) = escribe código, refactoriza y corre pruebas.
Seguir la skill `delegar-codex`: plan breve → prompt corto a Codex con rutas exactas → revisar `git diff` y la vista con
el plugin de Playwright (tras `--build frontend`) → subir a `develop` cuando el usuario lo pida.
