using Siha.MsC.Api.Domain;

namespace Siha.MsC.Api.Services;

/// <summary>
/// Contrato para el extractor de datos desde documentos PDF (US-07, S2-P01, S2-P02).
/// Identifica créditos, horas, asignaturas y temarios, calculando la métrica de confianza
/// y detectando si el archivo es escaneado para derivarlo al flujo de OCR/LLM.
/// </summary>
public interface IExtractorPdfService
{
    /// <summary>
    /// Extrae datos a partir de una cadena de texto (capa de texto de PDF).
    /// </summary>
    /// <param name="contenidoTexto">Texto extraído del documento.</param>
    /// <param name="tieneCapaTexto">Indica si el documento poseía capa de texto nativa.</param>
    /// <param name="codigoAsignaturaFallback">Código opcional de referencia si no se detecta en el texto.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<ExtraccionResultado> ExtraerDeTextoAsync(
        string? contenidoTexto,
        bool tieneCapaTexto,
        string? codigoAsignaturaFallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extrae datos a partir de un archivo PDF en el almacenamiento compartido.
    /// </summary>
    /// <param name="rutaRelativaAlmacen">Ruta relativa dentro del volumen de almacenamiento.</param>
    /// <param name="codigoAsignaturaFallback">Código opcional de referencia.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<ExtraccionResultado> ExtraerDeArchivoAsync(
        string rutaRelativaAlmacen,
        string? codigoAsignaturaFallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Evalúa la confianza global de los datos extraídos (0.00 a 1.00).
    /// </summary>
    decimal CalcularConfianza(
        int? creditos,
        int? horas,
        IReadOnlyCollection<UnidadExtraida> unidades,
        string? nombreAsignatura,
        string? codigoAsignatura);

    /// <summary>
    /// Detecta si un texto representa un documento escaneado (sin texto o solo ruido).
    /// </summary>
    bool EsPdfEscaneado(string? texto, int cantidadPaginas = 1);
}
