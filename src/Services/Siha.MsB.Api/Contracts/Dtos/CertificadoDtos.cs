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
    string CarreraOrigen,
    string CarreraDestino,
    IReadOnlyCollection<MateriaCursadaResponse> Materias,
    IReadOnlyCollection<SolicitudHomologacionResponse> Solicitudes,
    IReadOnlyCollection<RevisorResponse> Revisores);

/// <summary>Materia del historial académico (aprobada o reprobada).</summary>
public record MateriaCursadaResponse(string Nombre, int Nivel, string PeriodoAcademico, decimal Nota, bool Aprobada);

/// <summary>Materia(s) aprobada(s) que pide homologar por una materia de la carrera de destino.</summary>
public record SolicitudHomologacionResponse(int Numero, IReadOnlyCollection<string> MateriasOrigen, string MateriaDestino);

/// <summary>Respuesta del registro por certificado: expediente creado y publicado (o en reintento).</summary>
public record SolicitudCertificadoResponse(
    ExpedienteResponse Expediente,
    CertificadoLeidoResponse Certificado,
    bool Publicado,
    string Mensaje);
