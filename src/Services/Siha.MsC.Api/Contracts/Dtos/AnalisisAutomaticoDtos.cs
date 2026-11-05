namespace Siha.MsC.Api.Contracts.Dtos;

/// <summary>Asignatura de una malla oficial (catálogo de MS A) con sus unidades: lo que "sabe" el agente.</summary>
public record DestinoAnalisisRequest(
    string Codigo,
    string Nombre,
    int Creditos,
    int? Semestre,
    IReadOnlyCollection<UnidadCatalogoRequest> Unidades);

public record UnidadCatalogoRequest(int Numero, string Nombre, int Horas, string? Contenidos);

/// <summary>
/// Estimación del agente para una solicitud con certificado SIHA: reglas del instructivo y, por cada
/// materia que el estudiante pide homologar, la comparación de contenidos, profundidad y carga horaria
/// con su explicación en lenguaje sencillo.
/// </summary>
public record AnalisisAutomaticoResponse(
    Guid ExpedienteId,
    IReadOnlyCollection<RequisitoResponse> Requisitos,
    IReadOnlyCollection<EvaluacionMateriaResponse> Evaluaciones,
    IReadOnlyCollection<string> Alertas,
    string Resumen,
    /// <summary>Trazabilidad técnica (reglas y versión aplicadas). No se muestra en pantalla.</summary>
    string DetalleTrazabilidad);

/// <summary>Regla del instructivo de homologación y si la solicitud la cumple, con la razón.</summary>
public record RequisitoResponse(string Regla, bool Cumple, string Explicacion);

public record EvaluacionMateriaResponse(
    int Numero,
    /// <summary>Materia de la carrera de destino tal como la pide el certificado.</summary>
    string MateriaDestino,
    string? CodigoDestino,
    int? NivelDestino,
    int? CreditosDestino,
    int HorasDestino,
    IReadOnlyCollection<MateriaOrigenEvaluadaResponse> MateriasOrigen,
    /// <summary>% de horas de la materia de destino cuyos temas están cubiertos.</summary>
    decimal Contenidos,
    /// <summary>% de horas dedicadas a esos temas en lo aprobado frente a lo que exige el destino.</summary>
    decimal Profundidad,
    /// <summary>% de horas aprobadas frente a las horas de la materia de destino (máximo 100).</summary>
    decimal CargaHoraria,
    /// <summary>CUMPLE (los tres criterios llegan al mínimo) o CRITERIO_DOCENTE.</summary>
    string Estimacion,
    IReadOnlyCollection<CoberturaUnidadPropuestaResponse> Unidades,
    /// <summary>Explicación paso a paso de cómo se llegó al resultado.</summary>
    IReadOnlyCollection<string> Explicacion,
    string Conclusion,
    IReadOnlyCollection<PrecedenteResponse> Precedentes,
    /// <summary>
    /// True cuando el agente no tiene el sílabo de origen o de destino (otra institución, materia que no
    /// está en la malla o sin unidades): no hay porcentajes y deciden los docentes.
    /// </summary>
    bool SinSilabo = false);

public record MateriaOrigenEvaluadaResponse(
    string Nombre,
    int Nivel,
    string PeriodoAcademico,
    decimal Nota,
    int? Horas,
    string? CodigoCatalogo,
    bool EncontradaEnMalla);

/// <summary>Cobertura de una unidad de destino y la unidad aprobada que mejor la cubre.</summary>
public record CoberturaUnidadPropuestaResponse(
    int Numero,
    string Nombre,
    int Horas,
    double Similitud,
    bool Cubierta,
    string? MateriaOrigen,
    int? NumeroUnidadOrigen,
    string? NombreUnidadOrigen,
    int? HorasUnidadOrigen);

/// <summary>Decisión de un docente revisor sobre una materia de destino.</summary>
public record DecisionMateriaRequest(string CodigoDestino, bool Aprobada, string? Observacion);

/// <summary>Respuesta final de un docente revisor (está o no de acuerdo con el agente, materia por materia).</summary>
public record DictamenRequest(IReadOnlyCollection<DecisionMateriaRequest> Decisiones, string? Comentario);

public record DictamenResponse(
    string CorreoDocente,
    string NombreDocente,
    IReadOnlyCollection<DecisionMateriaRequest> Decisiones,
    string? Comentario,
    DateTimeOffset FechaUtc);

public record RevisorAnalisisResponse(string Nombre, string Correo, bool Respondio);

/// <summary>
/// Resultado combinado por materia de destino: estimación del agente (CUMPLE o CRITERIO_DOCENTE)
/// y decisión de los dos revisores (PENDIENTE, HOMOLOGADA, NO_HOMOLOGADA o SIN_ACUERDO).
/// </summary>
public record ResultadoFinalMateriaResponse(
    string CodigoDestino,
    string NombreDestino,
    string Estimacion,
    int Aprobaciones,
    int Rechazos,
    string Resultado);

/// <summary>Materia del historial del certificado (aprobada o reprobada).</summary>
public record MateriaHistorialResponse(string Nombre, int Nivel, string PeriodoAcademico, decimal Nota, bool Aprobada);

/// <summary>Todo lo que ve el operador o el docente al abrir una solicitud.</summary>
public record SolicitudAnalisisResponse(
    Guid ExpedienteId,
    string CodigoExpediente,
    string NumeroCertificado,
    string EstudianteNombre,
    string TipoTramite,
    string RutaNormativa,
    string InstitucionOrigen,
    string CarreraOrigen,
    string CarreraDestino,
    IReadOnlyCollection<MateriaHistorialResponse> Historial,
    IReadOnlyCollection<RevisorAnalisisResponse> Revisores,
    /// <summary>PENDIENTE (el agente está analizando), ANALIZADO o ERROR.</summary>
    string Estado,
    string? Mensaje,
    DateTimeOffset? AnalizadoUtc,
    AnalisisAutomaticoResponse? Resultado,
    IReadOnlyCollection<DictamenResponse> Dictamenes,
    IReadOnlyCollection<ResultadoFinalMateriaResponse> ResultadoFinal,
    /// <summary>PENDIENTE (0 de 2 respuestas), EN_REVISION (1 de 2) o RESUELTA (2 de 2).</summary>
    string EstadoRevision,
    /// <summary>True si quien consulta es uno de los revisores y aún puede responder.</summary>
    bool PuedeResponder);
