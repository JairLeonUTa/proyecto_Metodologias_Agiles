using System.ComponentModel.DataAnnotations;

namespace Siha.MsB.Api.Contracts.Dtos;

public record DocumentoRequest(
    [param: Required, MaxLength(255)] string NombreArchivo,
    [param: Required, MaxLength(500)] string RutaAlmacenamiento,
    [param: MaxLength(64)] string? HashSha256,
    long TamanoBytes);

public record AsignaturaRequest(
    [param: Required, MaxLength(30)] string CodigoOrigen,
    [param: Required, MaxLength(200)] string NombreOrigen,
    [param: MaxLength(30)] string? CodigoDestino,
    double? Nota,
    DateOnly? FechaAprobacionCertificada);

/// <summary>
/// Petición de registro de un expediente. La validación exhaustiva de PDF, almacenamiento
/// seguro y permisos corresponde a las tareas S1-S05/S1-S06 (Microservicio B, Sprint 1);
/// aquí se cubre lo necesario para la persistencia y la publicación del evento (S1-J07/J08).
/// </summary>
public record CrearExpedienteRequest(
    [param: Required, MaxLength(20)] string TipoTramite,
    [param: Required, MaxLength(20)] string RutaNormativa,
    [param: Required, MaxLength(20)] string CarreraOrigenCodigo,
    [param: Required, MaxLength(20)] string CarreraDestinoCodigo,
    [param: MaxLength(150)] string? InstitucionOrigen,
    IReadOnlyCollection<DocumentoRequest>? Documentos,
    IReadOnlyCollection<AsignaturaRequest>? Asignaturas);

public record AsignaturaResponse(
    string CodigoOrigen,
    string NombreOrigen,
    string? CodigoDestino,
    double? Nota,
    DateOnly? FechaAprobacionCertificada);

public record ExpedienteResponse(
    Guid Id,
    string Codigo,
    string TipoTramite,
    string RutaNormativa,
    string CarreraOrigenCodigo,
    string CarreraDestinoCodigo,
    string Estado,
    Guid CorrelationId,
    bool EventoPublicado,
    int IntentosPublicacion,
    string? UltimoErrorPublicacion,
    DateTimeOffset FechaCreacionUtc,
    DateTimeOffset? FechaPublicacionUtc,
    IReadOnlyCollection<DocumentoResponse> Documentos,
    IReadOnlyCollection<AsignaturaResponse> Asignaturas);

/// <summary>
/// Respuesta del registro: la API devuelve HTTP 202 Accepted con el estado de procesamiento
/// (US-06, CA2) y el identificador de correlación para el seguimiento (CA3).
/// </summary>
public record ExpedienteAceptadoResponse(
    Guid Id,
    string Codigo,
    string Estado,
    Guid CorrelationId,
    bool Publicado,
    string Mensaje);
