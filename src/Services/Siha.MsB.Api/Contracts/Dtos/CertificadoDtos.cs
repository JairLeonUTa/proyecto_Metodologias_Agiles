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
    IReadOnlyCollection<RevisorResponse> Revisores,
    /// <summary>Sílabos de la otra universidad adjuntados y leídos (solo movilidad externa).</summary>
    IReadOnlyCollection<SilaboLeidoResponse>? Silabos = null,
    /// <summary>Materias de destino que no aparecen en la malla vigente, con el nombre oficial sugerido.</summary>
    IReadOnlyCollection<AvisoMateriaDestinoResponse>? Avisos = null);

/// <summary>
/// La materia de destino pedida no aparece en la malla vigente. No impide registrar (la malla puede estar
/// incompleta), pero el agente no podrá compararla: conviene corregir el nombre en el certificado.
/// </summary>
public record AvisoMateriaDestinoResponse(int Solicitud, string MateriaDestino, string? Sugerencia, string Mensaje);

/// <summary>
/// Respuesta 422 cuando el certificado no se puede registrar. <c>Tipo</c>: NO_ES_CERTIFICADO,
/// CAMPO_VACIO, REGLA o CAMPOS_Y_REGLAS. <c>Detalles</c> conserva la lista simple de mensajes.
/// </summary>
public record RechazoCertificadoResponse(
    string Mensaje,
    string Tipo,
    IReadOnlyCollection<string> Detalles,
    IReadOnlyCollection<ProblemaCertificadoResponse> Problemas,
    IReadOnlyCollection<SeccionVerificadaResponse> Secciones);

/// <summary>Un problema del certificado con su sección y tipo.</summary>
public record ProblemaCertificadoResponse(string Seccion, string Tipo, string Mensaje);

/// <summary>Una fila de la lista de verificación: la sección está bien (✔) o tiene problemas (✘).</summary>
public record SeccionVerificadaResponse(string Seccion, bool Ok, IReadOnlyCollection<string> Problemas);

/// <summary>Resumen de un sílabo externo leído: cuántas unidades y horas se encontraron.</summary>
public record SilaboLeidoResponse(
    string Materia,
    string? NombreEnSilabo,
    int? Creditos,
    int Unidades,
    int HorasTotales);

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

/// <summary>Materia del historial digitada en el registro manual.</summary>
public record MateriaManualRequest(string? Nombre, int? Nivel, string? PeriodoAcademico, decimal? Nota, bool Aprobada);

/// <summary>Una materia de destino y las materias aprobadas con las que se pide homologarla.</summary>
public record SolicitudManualRequest(IReadOnlyList<string>? MateriasOrigen, string? MateriaDestino);

/// <summary>
/// Registro manual de una solicitud, sin PDF: los mismos datos del certificado SIHA, digitados por el
/// operador. Pasa por las mismas validaciones que el certificado.
/// </summary>
public record RegistroManualRequest(
    string? EstudianteNombre,
    string? Cedula,
    /// <summary>"Cambio de carrera" o "Movilidad externa".</summary>
    string? TipoTramite,
    string? InstitucionOrigen,
    string? CarreraOrigen,
    string? CarreraDestino,
    string? Revisor1Correo,
    string? Revisor2Correo,
    IReadOnlyList<MateriaManualRequest>? Materias,
    IReadOnlyList<SolicitudManualRequest>? Solicitudes);

public record DocenteOpcionResponse(string Nombre, string Correo);

/// <summary>Opciones para llenar el registro manual: docentes revisores y materias de cada carrera.</summary>
public record OpcionesRegistroManualResponse(
    IReadOnlyList<DocenteOpcionResponse> Docentes,
    IReadOnlyList<string> MateriasOrigen,
    IReadOnlyList<string> MateriasDestino);
