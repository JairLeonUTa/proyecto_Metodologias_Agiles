using Siha.MsB.Api.Domain;
using Siha.MsB.Api.Domain.Entities;

namespace Siha.MsB.Api.Contracts.Dtos;

/// <summary>
/// Metadatos de un documento asociado al expediente (tabla EXPEDIENTE_DOC), incluido su
/// estado de recepción (S1-S06).
/// </summary>
public record DocumentoResponse(
    Guid Id,
    string NombreArchivo,
    string NombreAlmacenado,
    string RutaAlmacenamiento,
    string HashSha256,
    long TamanoBytes,
    string MimeType,
    string EstadoRecepcion,
    string? ObservacionValidacion,
    DateTimeOffset FechaCargaUtc);

/// <summary>
/// Respuesta de <c>GET /api/expedientes/{id}/documentos</c>: metadatos de los documentos
/// cargados más un resumen del estado de recepción.
/// </summary>
public record DocumentosExpedienteResponse(
    Guid ExpedienteId,
    string CodigoExpediente,
    string EstadoExpediente,
    int TotalDocumentos,
    int DocumentosValidados,
    int DocumentosRechazados,
    IReadOnlyCollection<DocumentoResponse> Documentos);

/// <summary>Mapeo de las entidades de expediente a sus DTOs de respuesta.</summary>
public static class MapeosExpediente
{
    public static DocumentoResponse ADto(ExpedienteDocumento documento) => new(
        documento.Id,
        documento.NombreArchivo,
        documento.NombreAlmacenado,
        documento.RutaAlmacenamiento,
        documento.HashSha256,
        documento.TamanoBytes,
        documento.MimeType,
        documento.EstadoRecepcion,
        documento.ObservacionValidacion,
        documento.FechaCargaUtc);

    public static DocumentosExpedienteResponse ADto(Expediente expediente, IReadOnlyCollection<DocumentoResponse> documentos) => new(
        expediente.Id,
        expediente.Codigo,
        expediente.Estado,
        documentos.Count,
        documentos.Count(d => d.EstadoRecepcion == EstadosRecepcionDocumento.Validado),
        documentos.Count(d => d.EstadoRecepcion == EstadosRecepcionDocumento.Rechazado),
        documentos);
}
