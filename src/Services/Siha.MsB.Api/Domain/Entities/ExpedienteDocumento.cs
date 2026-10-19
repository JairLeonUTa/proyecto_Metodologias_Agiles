namespace Siha.MsB.Api.Domain.Entities;

/// <summary>
/// Metadatos de un documento PDF asociado a un expediente (tabla EXPEDIENTE_DOC en DB B).
/// El archivo binario no viaja en el evento; solo se referencia por identificador y ruta.
///
/// La carga real de archivos (validación de firma %PDF, tamaño, almacenamiento seguro y hash
/// SHA-256) se resuelve en <c>POST /api/expedientes/{id}/documentos</c> (S1-S05 / S1-S06).
/// </summary>
public class ExpedienteDocumento
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ExpedienteId { get; set; }
    public Expediente? Expediente { get; set; }

    /// <summary>Nombre original declarado por el cliente (solo informativo).</summary>
    public string NombreArchivo { get; set; } = string.Empty;

    /// <summary>Nombre generado en disco (GUID + extensión); nunca proviene del cliente.</summary>
    public string NombreAlmacenado { get; set; } = string.Empty;

    /// <summary>Ruta relativa al directorio configurado en <c>Archivos:RutaAlmacenamiento</c>.</summary>
    public string RutaAlmacenamiento { get; set; } = string.Empty;

    /// <summary>Hash SHA-256 (hexadecimal, 64 caracteres) del contenido almacenado.</summary>
    public string HashSha256 { get; set; } = string.Empty;

    public long TamanoBytes { get; set; }

    /// <summary>Tipo MIME declarado y aceptado para el documento.</summary>
    public string MimeType { get; set; } = string.Empty;

    /// <summary>Estado de recepción (ver <see cref="EstadosRecepcionDocumento"/>).</summary>
    public string EstadoRecepcion { get; set; } = EstadosRecepcionDocumento.Validado;

    /// <summary>Motivo del rechazo cuando <see cref="EstadoRecepcion"/> es RECHAZADO.</summary>
    public string? ObservacionValidacion { get; set; }

    public DateTimeOffset FechaCargaUtc { get; set; } = DateTimeOffset.UtcNow;
}
