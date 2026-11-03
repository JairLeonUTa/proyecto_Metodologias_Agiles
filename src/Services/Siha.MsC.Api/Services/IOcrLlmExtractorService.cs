using Siha.MsC.Api.Domain;

namespace Siha.MsC.Api.Services;

/// <summary>
/// Contrato para la extracción ampliada OCR + LLM estructurado con directriz anti-alucinación (US-10, S2-P05).
/// </summary>
public interface IOcrLlmExtractorService
{
    /// <summary>
    /// Procesa el texto obtenido de un OCR sobre documento escaneado utilizando un prompt estructurado
    /// sin inventar datos ni alucinar valores no presentes.
    /// </summary>
    Task<ExtraccionResultado> ExtraerConLlmAsync(
        string textoOcr,
        string? codigoAsignaturaReferencia = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Construye el prompt estricto anti-alucinación para el LLM.
    /// </summary>
    string ConstruirPromptAntiAlucinacion(string textoOcr);
}
