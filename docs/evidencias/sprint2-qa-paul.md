# Sprint 2 — Aseguramiento de Calidad Funcional e Inteligencia Artificial (Paul Acurio)

**Rol:** Product Owner Proxy & AI Lead  
**Rama:** `feature/sprint2-paul`, basada en `develop`.  
**Fecha de corte:** 3 de noviembre de 2026.  
**Plataforma de ejecución:** .NET 8.0, C#, xUnit, FluentAssertions, Moq, Entity Framework Core InMemory.

---

## 1. Resumen Ejecutivo de Pruebas

| Suite | Tipo | Resultado | Estado |
|---|---|:---:|:---:|
| `Siha.MsC.Tests` | Unitarias y funcionales | **143 / 143** | **En verde (100%)** |
| `Siha.MsA.Tests` | Unitarias de catálogo y reglas | 113 / 113 | En verde |
| `Siha.MsB.Tests` | Unitarias de expedientes y PDFs | 67 / 67 | En verde |

*Compilación de la solución:* **0 advertencias, 0 errores**.

---

## 2. Detalle de Tareas de Paul Acurio (Sprint 2)

| Tarea | Historia | Estimación | Estado | Qué se entregó |
|---|---|:---:|:---:|---|
| **S2-P01** | US-07 | 5 h | **Completada** | Servicio extractor de PDF nativo (`IExtractorPdfService` / `ExtractorPdfService`). Extrae código, nombre, créditos, horas y temario de unidades. Calcula la métrica de confianza: si $\ge 0.80$ y campos completos $\rightarrow$ auto-validada (`CONFIRMADA`); si $< 0.80$ $\rightarrow$ deriva a revisión humana (`PENDIENTE_CONFIRMACION`). |
| **S2-P02** | US-07 | 3 h | **Completada** | Detector de documentos escaneados (`EsPdfEscaneado`). Si el PDF carece de capa de texto nativa o contiene ruido binario, se deriva al estado `PENDIENTE_OCR_LLM` (CA3 de US-07) con confianza $0.0$, garantizando que el sistema no invente datos ni continúe a ciegas. |
| **S2-P03** | US-08 | 3 h | **Completada** | Suite `ReglasGrpcDinamicasCaidaTests`. Valida que al modificar dinámicamente umbrales ($\theta, \delta, \tau$) en MS A, la siguiente evaluación en MS C adopta de inmediato el nuevo valor y su versión (CA2). Verifica tolerancia a caídas: si MS A está caído, se produce un bloqueo seguro (`ErrorReglasGrpc`) con trazabilidad completa del error (CA3). |
| **S2-P04** | US-09 | 5 h | **Completada** | Suite `PruebasNormativasCreditosTests`. Casos normativos evaluados: $4 \text{ vs } 3 \rightarrow 100\%$, $2 \text{ vs } 3 \rightarrow 66.67\%$, denominador nulo $\rightarrow$ rechazo controlado. Vigencia $> 10$ años en ruta `CAU-P-011` $\rightarrow$ derivación a `VALIDACION_CONOCIMIENTOS` (RN-03). 3.ª matrícula en malla destino $\rightarrow$ bloqueo duro `BLOQUEO_3RA_MATRICULA` (RN-16). Créditos externos $\rightarrow$ advertencia RN-15. |
| **S2-P05** | US-10 | 4 h | **Completada** | Servicio `IOcrLlmExtractorService` / `OcrLlmExtractorService`. Integra prompt estructurado para Ollama con directriz obligatoria de **CERO ALUCINACIÓN**: ante campos ilegibles o ausentes se responde `null`, penalizando la confianza para forzar la validación humana en `PENDIENTE_CONFIRMACION`. |
| **S2-P06** | US-10 | 4 h | **Completada** | Calibración de embeddings y evaluación semántica con muestras reales de la FISEI (`OcrLlmYCalibracionSemanticaTests`). Se comprobó que asignaturas homólogas (ej. Fundamentos de Programación vs Programación Estructurada) superan el umbral $\tau = 0.75$, mientras que materias no afines (Base de Datos vs Física) obtienen similitud nula ($0\%$). |
| **S2-P07** | US-10 | 5 h | **Completada** | Suite `PruebasFuncionalesCoberturaN1Tests`. Cobertura combinada $N:1$ (Física I + Física II $\rightarrow$ Física General) asegurando unión matemática de unidades cubiertas sin doble conteo. Comprobación del caso de aceptación: 6 de 7 unidades cubiertas $\rightarrow 85.71\%$ ponderado por horas, con detección exacta de la unidad faltante (U.7). Control de RN-19 contra duplicidad de asignaturas de origen. |
| **S2-P08** | US-11 | 3 h | **Completada** | Carga de resoluciones históricas oficiales (ej. `RES-FISEI-2025-031`) en la base de datos de precedentes y verificación de consultas exactas (`PrecedentesYControlesNormativosTests`). Coincidencias exactas retornan el precedente; variaciones parciales devuelven `[]` vacío sin inventar antecedentes. |
| **S2-P09** | US-11 | 2 h | **Completada** | Verificación de jerarquía normativa ante precedentes favorables: aun existiendo un precedente histórico favorable idéntico, si la materia tiene $> 10$ años obligatoriamente deriva a `VALIDACION_CONOCIMIENTOS` (RN-03) y exige dictamen docente. Si el estudiante cursa tercera matrícula, se impone el bloqueo duro de RN-16. |
| **S2-P10** | US-11 | 2 h | **Completada** | Suite de regresión funcional completa `RegresionFuncionalSprint2Tests`. Validación de los flujos de extremo a extremo: Trámite Interno (catálogo directo y auto-confirmación) y Trámite Externo con PDF escaneado (detección $\rightarrow$ OCR/LLM $\rightarrow$ confirmación del operador $\rightarrow$ orquestación del análisis con precedentes y trazabilidad). |

---

## 3. Criterios de Aceptación Validados

### US-07: Extracción Base de PDF y Confirmación Humana
- **CA1:** Extracción genera datos estructurados vinculados al documento.
- **CA2:** Si confianza $< 0.80$ o faltan créditos/horas/unidades, el expediente se bloquea en `PENDIENTE_CONFIRMACION`.
- **CA3:** PDF escaneado o sin capa de texto interpretable es identificado como `PENDIENTE_OCR_LLM` sin inventar créditos ni temas.

### US-08: Reglas vía gRPC
- **CA1:** Consulta devuelve $\theta, \delta, \tau$ vigentes asociados a la ruta normativa.
- **CA2:** Actualización dinámica de parámetros en MS A es reflejada de inmediato en la siguiente evaluación de MS C con indicación de su versión.
- **CA3:** Indisponibilidad de MS A deriva en bloqueo seguro `ErrorReglasGrpc` trazado con el error, sin sustitución silenciosa de valores.

### US-09: Proporción de Créditos ($P_{cred}$) y Controles Normativos
- **CA1:** $P_{cred}$ correcto ($4 \text{ vs } 3 \rightarrow 100\%$, $2 \text{ vs } 3 \rightarrow 66.67\%$). Denominador cero arroja error controlado.
- **CA2:** Asignatura $> 10$ años en ruta `CAU-P-011` deriva a `VALIDACION_CONOCIMIENTOS` (RN-03).
- **CA3:** Tercera matrícula en destino genera bloqueo duro (RN-16). Créditos externos disparan advertencia de no aplicar conversión automática de 48h (RN-15).

### US-10: Extracción Ampliada (OCR + LLM) y Similitud Semántica ($P_{cont}$)
- **CA1:** Prompt anti-alucinación estricto: campos no visibles quedan en `null`.
- **CA2:** Unidad destino cubierta si $\max(\cos) \ge \tau$ ($0.75$). Ponderación por horas exacta ($6/7 \rightarrow 85.71\%$).
- **CA3:** Cobertura $N:1$ sin doble conteo. Verificación de RN-19.

### US-11: Precedentes y Cierre
- **CA1:** Coincidencia exacta devuelve el precedente oficial (`RES-FISEI-2025-031`).
- **CA2:** Sin coincidencia devuelve `[]` vacío.
- **CA3:** Precedente favorable no omite controles normativos de 10 años ni tercera matrícula.

---

## 4. Instrucciones de Reproducción

Para compilar y ejecutar la suite completa de pruebas:

```bash
# Restaurar dependencias
dotnet restore

# Ejecutar las 143 pruebas del Microservicio C
dotnet test tests/Siha.MsC.Tests/Siha.MsC.Tests.csproj -v normal

# Ejecutar específicamente las pruebas funcionales de Paul Acurio
dotnet test tests/Siha.MsC.Tests/Siha.MsC.Tests.csproj --filter "FullyQualifiedName~ExtractorPdfServiceTests|FullyQualifiedName~ReglasGrpcDinamicasCaidaTests|FullyQualifiedName~PruebasNormativasCreditosTests|FullyQualifiedName~OcrLlmYCalibracionSemanticaTests|FullyQualifiedName~PruebasFuncionalesCoberturaN1Tests|FullyQualifiedName~PrecedentesYControlesNormativosTests|FullyQualifiedName~RegresionFuncionalSprint2Tests"
```
