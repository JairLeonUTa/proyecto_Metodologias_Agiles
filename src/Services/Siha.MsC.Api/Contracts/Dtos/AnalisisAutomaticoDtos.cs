namespace Siha.MsC.Api.Contracts.Dtos;

/// <summary>
/// Análisis automático de un expediente completo: compara los contenidos de TODAS las asignaturas
/// aprobadas del certificado con TODAS las asignaturas de la carrera de destino y propone solo las
/// homologaciones que alcanzan el mínimo de contenido vigente.
/// </summary>
public record AnalisisAutomaticoRequest(
    string RutaNormativa,
    string TipoTramite,
    string InstitucionOrigen,
    IReadOnlyCollection<OrigenAnalisisRequest> AsignaturasOrigen,
    IReadOnlyCollection<DestinoAnalisisRequest> AsignaturasDestino);

/// <summary>Datos del certificado (MS B) que no están en la extracción: fecha y matrícula.</summary>
public record OrigenAnalisisRequest(string Codigo, DateOnly? FechaAprobacion, int? NumeroMatricula);

/// <summary>Asignatura de la malla de destino con su plan analítico (catálogo de MS A).</summary>
public record DestinoAnalisisRequest(
    string Codigo,
    string Nombre,
    int Creditos,
    int? Semestre,
    IReadOnlyCollection<UnidadCatalogoRequest> Unidades);

public record UnidadCatalogoRequest(int Numero, string Nombre, int Horas, string? Contenidos);

public record AnalisisAutomaticoResponse(
    Guid ExpedienteId,
    /// <summary>Homologaciones que alcanzan el contenido mínimo (cada materia de origen se usa una sola vez).</summary>
    IReadOnlyCollection<PropuestaHomologacionResponse> Propuestas,
    /// <summary>Materias de destino con coincidencias parciales que NO alcanzan el mínimo.</summary>
    IReadOnlyCollection<PropuestaHomologacionResponse> SinCoberturaSuficiente,
    /// <summary>Materias aprobadas que no sirven para ninguna materia de destino.</summary>
    IReadOnlyCollection<OrigenPropuestaResponse> OrigenesSinAsignar,
    IReadOnlyCollection<string> Alertas,
    /// <summary>Trazabilidad técnica (reglas y versiones aplicadas). No se muestra en pantalla.</summary>
    string DetalleTrazabilidad);

public record PropuestaHomologacionResponse(
    string CodigoDestino,
    string NombreDestino,
    int CreditosDestino,
    int? SemestreDestino,
    IReadOnlyCollection<OrigenPropuestaResponse> Origenes,
    decimal Pcont,
    decimal Pcred,
    string CategoriaCreditos,
    bool AlcanzaContenido,
    EstadoEvaluacionFinal EstadoFinal,
    IReadOnlyCollection<CoberturaUnidadPropuestaResponse> Unidades,
    IReadOnlyCollection<string> Alertas,
    IReadOnlyCollection<PrecedenteResponse> Precedentes);

public record OrigenPropuestaResponse(
    Guid ExtraccionId,
    string Codigo,
    string Nombre,
    int? Creditos,
    int? Horas,
    DateOnly? FechaAprobacion,
    int? AnosTranscurridos,
    int? NumeroMatricula);

/// <summary>Cobertura de una unidad de destino y la unidad de origen que mejor la cubre.</summary>
public record CoberturaUnidadPropuestaResponse(
    int Numero,
    string Nombre,
    int Horas,
    double Similitud,
    bool Cubierta,
    string? CodigoOrigen,
    int? NumeroUnidadOrigen,
    string? NombreUnidadOrigen);
