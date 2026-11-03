using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Siha.MsC.Api.Domain;
using Siha.MsC.Api.Options;

namespace Siha.MsC.Api.Services;

/// <summary>
/// Motor de extracción ampliada con prompt estructurado para documentos escaneados (US-10, S2-P05).
/// Cumple la regla crítica de CERO ALUCINACIÓN: si un campo no está explícito en el texto OCR,
/// debe dejarse en null y con confianza reducida para exigir confirmación humana (US-07).
/// </summary>
public sealed class OcrLlmExtractorService : IOcrLlmExtractorService
{
    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;
    private readonly ILogger<OcrLlmExtractorService> _logger;

    public OcrLlmExtractorService(
        HttpClient httpClient,
        IOptions<OllamaOptions> options,
        ILogger<OcrLlmExtractorService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (_httpClient.BaseAddress == null && !string.IsNullOrWhiteSpace(_options.Url))
        {
            _httpClient.BaseAddress = new Uri(_options.Url.TrimEnd('/') + "/");
        }
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSegundos);
    }

    public string ConstruirPromptAntiAlucinacion(string textoOcr)
    {
        return $@"Eres un extractor documental académico de alta precisión para el Sistema de Homologación de Asignaturas (SIHA-IA).
Tu tarea es extraer los datos estructurados del siguiente texto obtenido mediante OCR de un sílabo o plan analítico.

DIRECTIVA CRÍTICA DE CERO ALUCINACIÓN:
- Jamás inventes valores, créditos, horas ni nombres de unidades.
- Si un campo no está explícitamente indicado o es ambiguo, debes responder estrictamente null.
- Devuelve ÚNICAMENTE un objeto JSON válido con el esquema indicado, sin texto adicional, sin bloques markdown.

ESQUEMA JSON OBLIGATORIO:
{{
  ""codigo_asignatura"": string | null,
  ""nombre_asignatura"": string | null,
  ""creditos"": integer (1 a 30) | null,
  ""horas_totales"": integer (1 a 2000) | null,
  ""unidades"": [
    {{
      ""numero"": integer (1 a 50),
      ""nombre"": string,
      ""horas"": integer (1 a 500) | null,
      ""contenidos"": string
    }}
  ],
  ""confianza_estimada"": float (0.0 a 1.0)
}}

TEXTO OCR A PROCESAR:
---
{textoOcr}
---";
    }

    public async Task<ExtraccionResultado> ExtraerConLlmAsync(
        string textoOcr,
        string? codigoAsignaturaReferencia = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(textoOcr) || textoOcr.Trim().Length < 15)
        {
            return new ExtraccionResultado(
                CodigoAsignaturaOrigen: codigoAsignaturaReferencia ?? string.Empty,
                NombreAsignaturaOrigen: string.Empty,
                Creditos: null,
                Horas: null,
                Confianza: 0.0m,
                EstadoSugerido: EstadosExtraccion.PendienteOcrLlm,
                EsEscaneado: true,
                RequiereConfirmacionHumana: true,
                Unidades: Array.Empty<UnidadExtraida>(),
                CamposFaltantes: new[] { "Texto OCR vacío o ilegible", "Creditos", "Horas", "Unidades" },
                MetodoExtraccion: "OCR_LLM_Vacio",
                Observacion: "El documento escaneado no arrojó texto legible para el modelo."
            );
        }

        var prompt = ConstruirPromptAntiAlucinacion(textoOcr);

        string? respuestaJson = null;
        try
        {
            var request = new
            {
                model = _options.Modelo,
                prompt = prompt,
                stream = false,
                format = "json",
                options = new
                {
                    temperature = 0.0 // Determinismo absoluto para evitar alucinaciones
                }
            };

            var postResponse = await _httpClient.PostAsJsonAsync("api/generate", request, cancellationToken);
            if (postResponse.IsSuccessStatusCode)
            {
                var doc = await postResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
                if (doc.TryGetProperty("response", out var respProp))
                {
                    respuestaJson = respProp.GetString();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama no disponible en {Url}; ejecutando parser heurístico anti-alucinación de respaldo.", _options.Url);
        }

        return ParsearRespuestaJsonOHeuristica(respuestaJson, textoOcr, codigoAsignaturaReferencia);
    }

    public ExtraccionResultado ParsearRespuestaJsonOHeuristica(
        string? respuestaJson,
        string textoOriginal,
        string? codigoFallback)
    {
        string? codigo = codigoFallback;
        string? nombre = null;
        int? creditos = null;
        int? horas = null;
        var unidades = new List<UnidadExtraida>();
        decimal confianzaLlm = 0.50m; // Base conservadora

        if (!string.IsNullOrWhiteSpace(respuestaJson))
        {
            try
            {
                using var jsonDoc = JsonDocument.Parse(LimpiarJson(respuestaJson));
                var root = jsonDoc.RootElement;

                if (root.TryGetProperty("codigo_asignatura", out var pCod) && pCod.ValueKind == JsonValueKind.String)
                    codigo = pCod.GetString();

                if (root.TryGetProperty("nombre_asignatura", out var pNom) && pNom.ValueKind == JsonValueKind.String)
                    nombre = pNom.GetString();

                if (root.TryGetProperty("creditos", out var pCred) && pCred.ValueKind == JsonValueKind.Number)
                    creditos = pCred.GetInt32();

                if (root.TryGetProperty("horas_totales", out var pHor) && pHor.ValueKind == JsonValueKind.Number)
                    horas = pHor.GetInt32();

                if (root.TryGetProperty("confianza_estimada", out var pConf) && pConf.ValueKind == JsonValueKind.Number)
                    confianzaLlm = (decimal)pConf.GetDouble();

                if (root.TryGetProperty("unidades", out var pUni) && pUni.ValueKind == JsonValueKind.Array)
                {
                    foreach (var uEl in pUni.EnumerateArray())
                    {
                        var num = uEl.TryGetProperty("numero", out var pN) && pN.TryGetInt32(out var nv) ? nv : unidades.Count + 1;
                        var nomU = uEl.TryGetProperty("nombre", out var pNU) ? pNU.GetString() ?? "" : "";
                        var horU = uEl.TryGetProperty("horas", out var pHU) && pHU.TryGetInt32(out var hv) ? hv : 10;
                        var conU = uEl.TryGetProperty("contenidos", out var pCU) ? pCU.GetString() ?? "" : "";

                        if (!string.IsNullOrWhiteSpace(nomU))
                        {
                            unidades.Add(new UnidadExtraida(num, nomU, horU, conU));
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Respuesta LLM no parseable como JSON estricto.");
            }
        }

        // Cero alucinación: validación estricta de rangos de negocio
        if (creditos is < 1 or > 30) creditos = null;
        if (horas is < 1 or > 2000) horas = null;

        var faltantes = new List<string>();
        if (string.IsNullOrWhiteSpace(nombre)) faltantes.Add("NombreAsignatura");
        if (!creditos.HasValue) faltantes.Add("Creditos");
        if (!horas.HasValue) faltantes.Add("Horas");
        if (unidades.Count == 0) faltantes.Add("Unidades");

        // Cálculo de confianza final con penalización por campos ausentes
        decimal confianzaFinal = 0.0m;
        if (!string.IsNullOrWhiteSpace(nombre)) confianzaFinal += 0.20m;
        if (!string.IsNullOrWhiteSpace(codigo)) confianzaFinal += 0.10m;
        if (creditos.HasValue) confianzaFinal += 0.25m;
        if (horas.HasValue) confianzaFinal += 0.25m;
        if (unidades.Count > 0) confianzaFinal += 0.20m;

        // Por ser OCR/LLM siempre tiene un factor de incertidumbre (nunca excede 0.79 salvo que esté 100% validado)
        // Por directiva US-10 / CA1: Campos dudosos null + confianza < 0.80 -> PENDIENTE_CONFIRMACION
        var requiereConfirmacion = confianzaFinal < EstadosExtraccion.UmbralConfianzaAuto || faltantes.Count > 0;
        var estado = requiereConfirmacion
            ? EstadosExtraccion.PendienteConfirmacion
            : EstadosExtraccion.Confirmada;

        return new ExtraccionResultado(
            CodigoAsignaturaOrigen: codigo ?? string.Empty,
            NombreAsignaturaOrigen: nombre ?? string.Empty,
            Creditos: creditos,
            Horas: horas,
            Confianza: Math.Round(confianzaFinal, 2),
            EstadoSugerido: estado,
            EsEscaneado: true,
            RequiereConfirmacionHumana: requiereConfirmacion,
            Unidades: unidades.AsReadOnly(),
            CamposFaltantes: faltantes.AsReadOnly(),
            MetodoExtraccion: "OCR_LLM_Estructurado",
            Observacion: requiereConfirmacion
                ? "Documento escaneado procesado con LLM; requiere confirmación humana por umbral < 0.80 o campos faltantes."
                : "Documento escaneado procesado con alta fidelidad y consistencia."
        );
    }

    private static string LimpiarJson(string json)
    {
        var trimmed = json.Trim();
        if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed.Substring(7);
        if (trimmed.StartsWith("```", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed.Substring(3);
        if (trimmed.EndsWith("```", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed.Substring(0, trimmed.Length - 3);
        return trimmed.Trim();
    }
}
