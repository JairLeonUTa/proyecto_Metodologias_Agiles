---
name: delegar-codex
description: Flujo de ahorro de tokens en SIHA-IA — Claude planea y revisa, Codex (mcp__codex__codex) escribe el código y corre lint y pruebas. Úsala cuando el usuario pida un cambio de código, de interfaz, una refactorización o correr pruebas.
---

# Flujo Claude (arquitecto/revisor) + Codex (ejecutor)

Claude no lee archivos enteros ni escribe código masivo. Planea, delega con un prompt corto y revisa el resultado.

## 1. Plan breve (Claude)
- Máximo 5 viñetas: qué cambia, en qué archivos exactos, y el **criterio de aceptación** visual y funcional.
- Si es interfaz, cargar antes `siha-ui` y `frontend-design` y citar las reglas que aplican.
- Si falta información, pedirla a Codex en modo `read-only` ("resume en ≤20 líneas …"), no leerla a mano.

## 2. Delegar a Codex
Llamar a `mcp__codex__codex` con `cwd = C:\Users\ASUS\Downloads\siha-sprint2` y `sandbox = "workspace-write"`
solo para cambios que el usuario ya pidió (para explorar, `read-only`). Plantilla del prompt:

```
Tarea: <una frase>.
Archivos: <rutas exactas>. No toques otros salvo tests nuevos junto a ellos.
Hacer:
- <paso 1>
- <paso 2>
Reglas: español sencillo en textos visibles, sin símbolos ni errores técnicos; usar componentes de
frontend/src/components/ui y tokens de tailwind.config.js; no cambiar la regla 80 % contenidos + 80 % créditos.
Pruebas: añade/ajusta Vitest junto al archivo. Luego en frontend/: `npm run lint` (Vitest no corre en el
sandbox de Codex; si falla por "Acceso denegado", solo avisa).
(Backend: `$env:DOTNET_ROLL_FORWARD='LatestMajor'; dotnet test tests\Siha.MsX.Tests`; migraciones EF siempre incrementales.)
No hagas commit ni push.
Responde en ≤15 líneas: archivos cambiados, resultado de lint y tests (números), dudas.
```

## 3. Revisión (Claude)
- `git diff --stat` y luego `git diff` solo de los archivos clave (no todo si es grande).
- Correr Vitest Claude mismo: `cd frontend; npx vitest run` (solo mostrar el resumen de Tests/Test Files).
- Para interfaz: reconstruir antes el frontend (`docker compose --profile sprint23 up -d --build frontend`,
  es una imagen nginx, no recarga sola) y abrir `http://localhost:5173` con el plugin de Playwright
  (herramientas `browser_*`; si no está, usar el navegador integrado de la app). Entrar con el rol
  afectado, capturar 1366×768 y 375 px y comparar con el criterio del paso 1.
- Revisar: textos técnicos o símbolos, colores fuera de tokens, estados `CUMPLE`/`NO_CUMPLE`/`CRITERIO_DOCENTE`
  correctos, `NO_CUMPLE` sin votación, pruebas añadidas.
- Si algo falla, devolver a Codex un prompt de corrección corto con el problema exacto (archivo:línea).

## 4. Subir a develop (solo cuando el usuario lo pida)
1. `git fetch` y `git log --oneline HEAD..origin/develop`: si hay commits nuevos, `git pull --rebase` y repetir pruebas.
2. Mensaje de commit en español (convención `feat:`/`fix:`/`test:`), en archivo y `git commit -F`,
   **sin** línea `Co-Authored-By` (el usuario no la quiere).
3. `git push origin develop` (el equipo sube directo, sin PR). Nunca subir `.env`.

## Ahorro de tokens
- No pegar salidas largas: pedir a Codex resúmenes con números.
- No releer archivos que Codex ya resumió; pedirle `archivo:línea` concretos.
- Un prompt a Codex por tarea coherente, no uno por archivo.
