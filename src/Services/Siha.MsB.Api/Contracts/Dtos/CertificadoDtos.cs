namespace Siha.MsB.Api.Contracts.Dtos;

/// <summary>Datos leídos del certificado SIHA (formato SIHA-CH-01).</summary>
public record CertificadoLeidoResponse(
    string NumeroCertificado,
    DateOnly FechaEmision,
    string EmitidoPor,
    string EstudianteNombre,
    string EstudianteIdentificacion,
    string TipoTramite,
    string RutaNormativa,
    string InstitucionOrigen,
    string CarreraOrigenCodigo,
    string CarreraOrigenNombre,
    string CarreraDestinoCodigo,
    string CarreraDestinoNombre,
    IReadOnlyCollection<AsignaturaCertificadaResponse> Asignaturas,
    IReadOnlyCollection<RevisorResponse> Revisores);

public record AsignaturaCertificadaResponse(
    string Codigo,
    string Nombre,
    int Semestre,
    int Creditos,
    int HorasTotales,
    DateOnly FechaAprobacion,
    decimal Nota,
    IReadOnlyCollection<UnidadCertificadaResponse> Unidades);

public record UnidadCertificadaResponse(int Numero, string Nombre, int Horas, string Contenidos);

/// <summary>Respuesta del registro por certificado: expediente creado y publicado (o en reintento).</summary>
public record SolicitudCertificadoResponse(
    ExpedienteResponse Expediente,
    CertificadoLeidoResponse Certificado,
    bool Publicado,
    string Mensaje);
