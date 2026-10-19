namespace Siha.MsB.Api.Domain;

/// <summary>
/// Estado de recepción de un documento asociado a un expediente (S1-S05 / S1-S06).
/// Permite al operador ver, en <c>GET /api/expedientes/{id}/documentos</c>, qué cargas
/// fueron aceptadas y cuáles rechazadas por la validación de PDF.
/// </summary>
public static class EstadosRecepcionDocumento
{
    /// <summary>El documento superó la validación (firma %PDF, tamaño, extensión y MIME) y quedó almacenado.</summary>
    public const string Validado = "VALIDADO";

    /// <summary>El documento fue rechazado por la validación; la carga devolvió 400 Bad Request.</summary>
    public const string Rechazado = "RECHAZADO";
}
