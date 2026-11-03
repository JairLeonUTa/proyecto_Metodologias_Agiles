namespace Siha.MsC.Api.Domain;

/// <summary>
/// Representa el resultado del análisis y extracción de datos desde un PDF (US-07, S2-P01, S2-P02).
/// </summary>
public sealed record ExtraccionResultado(
    string CodigoAsignaturaOrigen,
    string NombreAsignaturaOrigen,
    int? Creditos,
    int? Horas,
    decimal Confianza,
    string EstadoSugerido,
    bool EsEscaneado,
    bool RequiereConfirmacionHumana,
    IReadOnlyCollection<UnidadExtraida> Unidades,
    IReadOnlyCollection<string> CamposFaltantes,
    string MetodoExtraccion,
    string? Observacion = null);

/// <summary>
/// Unidad temática extraída del documento.
/// </summary>
public sealed record UnidadExtraida(
    int Numero,
    string Nombre,
    int Horas,
    string Contenidos);
