# Sprint 2 — Frontend (Alex Jonathan Chisag Punina)

**Rol:** Frontend Lead & UI/UX Specialist · **Rama:** `feature/sprint2-jonathan` · **Historias:** US-07 a US-11

Implementación de las pantallas React de los mockups M-01 a M-06 conectadas al Microservicio C
a través del API Gateway (`/api/extracciones/{everything}`).

## Tareas cumplidas

| Tarea | Historia | Entregable | Archivos principales |
|---|---|---|---|
| S2-A01 | US-07 | Bandeja de procesamiento con % de confianza y alerta "Confirmación pendiente" (M-01) | `pages/BandejaProcesamientoPage.tsx`, `components/analisis/IndicadorConfianza.tsx` |
| S2-A02 | US-07 | Formulario de edición/confirmación de créditos, horas y unidades; botón "Confirmar y calcular" (M-02) | `pages/ConfirmarExtraccionPage.tsx`, `utils/analisis.ts` (`validarConfirmacion`) |
| S2-A03 | US-07 | Integración con `PUT /api/extracciones/{id}/confirmar`; el cálculo queda bloqueado hasta confirmar todo; manejo de 400/409 | `api/extraccionesApi.ts`, `pages/ConfirmarExtraccionPage.tsx` |
| S2-A04 | US-09 | Panel de créditos: Pcred rotulado como "métrica informativa de apoyo" + alertas "Vigencia > 10 años" y "Tercera matrícula detectada" (M-03) | `components/analisis/PanelCreditos.tsx` |
| S2-A05 | US-10 | Vista de estado de la extracción ampliada: "OCR en curso", "LLM analizando", "Validación operador requerida"; se actualiza cada 10 s (M-04) | `pages/EstadoExtraccionPage.tsx`, `components/analisis/EtapasExtraccion.tsx` |
| S2-A06 | US-10 | Tabla de cobertura por unidad: similitud coseno, horas, ✓ cubierta / ✗ faltante (M-05) | `components/analisis/TablaCobertura.tsx` |
| S2-A07 | US-11 | Componente de precedentes: resolución, asignaturas comparadas, institución; `[]` → "Sin precedentes registrados" (M-06) | `components/analisis/ConsultaPrecedentes.tsx` |
| S2-A08 | US-08 | Integración de las vistas con MS C real: `POST /api/extracciones/{id}/analizar`, parámetros θ, δ, τ y versión de la regla | `pages/BandejaAnalisisPage.tsx`, `pages/AnalisisExpedientePage.tsx` |
| S2-A09 | US-11 | Manejo de errores de la UI: PDF ilegible → mensaje; datos sin confirmar → bloqueo; falla de precedentes → aviso que no bloquea el cálculo | pruebas `*.test.ts(x)` |
| S2-A10 | US-11 | Consistencia visual (tokens Tailwind del Sprint 1), navegación por teclado (pestañas WAI-ARIA), guion de demostración | `components/analisis/Pestanas.tsx`, este documento |

## Cambios mínimos en el Microservicio C (necesarios para la UI)

1. **`GET /api/extracciones/bandeja?estado=&expedienteId=`** (`ExtraccionesController.Bandeja`,
   `ExtraccionService.ListarAsync`): MS C solo permitía consultar una extracción por id; la bandeja
   (S2-A01) necesita el listado. Devuelve las 100 más recientes. Prueba nueva en `ExtraccionServiceTests`.
2. **`AgenteAnalizadorService`**: si una unidad de destino llega sin embedding (la web envía las unidades
   del catálogo sin vector), se genera con Ollama a partir de sus contenidos. Las llamadas que ya traen
   vector no cambian, así que las pruebas existentes de Jair y Sebastián no se ven afectadas.

## Rutas nuevas

| Ruta | Roles | Mockup |
|---|---|---|
| `/procesamiento` | Operador, Administrador | M-01 |
| `/procesamiento/:id/confirmar` | Operador, Administrador | M-02 |
| `/procesamiento/:id/estado` | Todos | M-04 |
| `/evaluacion` | Docente, Administrador | Bandeja de análisis |
| `/evaluacion/:id?vista=creditos\|cobertura\|precedentes` | Docente, Administrador | M-03, M-05, M-06 |

## Pruebas

```bash
cd frontend
npm test          # 34 pruebas: 11 del Sprint 1 + 23 nuevas del Sprint 2
npm run build     # typecheck + build de producción
```

Casos cubiertos: umbral 0,80, validación idéntica a `ExtraccionService.Validar`, bloqueo sin créditos,
PUT con datos corregidos, conflicto 409, PDF pendiente de OCR/LLM en solo lectura, caso 6/7 = 85,71 % con
U.7 faltante, precedente exacto RES-FISEI-2025-031, `[]` sin resoluciones inventadas, falla de precedentes
no bloqueante, alertas de vigencia y tercera matrícula, navegación con flechas en las pestañas.

Capturas (`docs/evidencias/sprint2-frontend/`) generadas con `node scripts/capturas-sprint2-frontend.cjs <carpeta>`
sobre `npm run preview` con respuestas simuladas del Gateway.

## Guion de demostración — Sprint Review (miércoles 4/11)

Duración sugerida: 6 minutos. Requisitos: `docker compose up -d --build`, Ollama con el modelo de embeddings,
un expediente interno con PDF de texto y uno externo con PDF escaneado.

1. **Operador — registrar** (`operador@uta.edu.ec`): Expedientes → Registrar solicitud; subir el plan analítico
   y enviar. Explicar que MS B publica `SolicitudCreadaEvent` (solo referencia + SHA-256).
2. **Bandeja de procesamiento (M-01):** mostrar la confianza de cada expediente; el de confianza 0,68 aparece
   como "Confirmación pendiente" y el escaneado como "Pendiente OCR / LLM".
3. **Estado OCR/LLM (M-04):** abrir "Ver estado" del PDF escaneado; señalar las cinco etapas y que los campos
   dudosos quedan **vacíos**, no inventados.
4. **Confirmar datos (M-02):** abrir "Confirmar datos"; intentar confirmar sin créditos → el formulario lo
   impide. Ingresar créditos, corregir una unidad y pulsar "Confirmar y calcular".
5. **Docente — análisis** (`docente@uta.edu.ec`): Evaluación → Bandeja de análisis → "Analizar". Ingresar
   la asignatura destino (p. ej. SW101), años y matrícula; ejecutar.
6. **Créditos y controles (M-03):** Pcred como métrica de apoyo, θ · δ · τ y versión de la regla vía gRPC.
   Repetir con 12 años y tercera matrícula para mostrar las alertas.
7. **Cobertura (M-05):** caso 6 de 7 unidades = 85,71 %, U.7 faltante.
8. **Precedentes (M-06):** coincidencia exacta RES-FISEI-2025-031; con otra institución → "Sin precedentes
   registrados".
9. **Accesibilidad:** recorrer las pestañas con ←/→ y mostrar el foco visible.
