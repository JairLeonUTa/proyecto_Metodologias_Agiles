namespace Siha.MsC.Api.Contracts.Dtos;

/// <summary>Unidad temática editable por el operador durante la confirmación.</summary>
public record UnidadExtraccionRequest(
    int Numero,
    string Nombre,
    int Horas,
    string? Contenidos = null);

/// <summary>
/// Correcciones del operador sobre la extracción (S2-J02). Los créditos, las horas y al menos
/// una unidad temática son obligatorios: sin ellos la evaluación no puede continuar.
/// </summary>
public record ConfirmarExtraccionRequest(
    int Creditos,
    int Horas,
    IReadOnlyCollection<UnidadExtraccionRequest>? Unidades,
    string? Observacion = null);

public record UnidadExtraccionResponse(
    int Numero,
    string Nombre,
    int Horas,
    string Contenidos);

public record ExtraccionResponse(
    Guid Id,
    Guid ExpedienteId,
    Guid? DocumentoId,
    string CodigoAsignaturaOrigen,
    string NombreAsignaturaOrigen,
    int? Creditos,
    int? Horas,
    decimal Confianza,
    string Estado,
    bool RequiereConfirmacion,
    IReadOnlyCollection<UnidadExtraccionResponse> Unidades,
    DateTimeOffset CreadoUtc,
    DateTimeOffset? ConfirmadoUtc,
    string? ConfirmadoPor,
    string? Observacion);

/// <summary>Parámetros para calcular la proporción de créditos (Pcred) y controles normativos (US-09, S2-J04).</summary>
public record CalcularCreditosRequest(
    int CreditosOrigen,
    int CreditosDestino,
    int AnosTranscurridos,
    int NumeroMatriculaDestino,
    bool EsCreditoExterno,
    string RutaNormativa);

/// <summary>Resultado del cálculo de Pcred y evaluación normativa.</summary>
public record EvaluacionCreditosResponse(
    decimal Pcred,
    string Categoria,
    IReadOnlyCollection<string> Alertas,
    bool BloqueoDuro,
    string DetalleCalculo);

/// <summary>Unidad de origen con embedding para evaluación de contenidos (US-10, S2-J05/J06).</summary>
public record UnidadOrigenEmbeddingRequest(
    int Numero,
    string Nombre,
    int Horas,
    string Contenidos,
    IReadOnlyCollection<float> Embedding);

/// <summary>Unidad de destino con embedding precalculado para evaluación de contenidos.</summary>
public record UnidadDestinoEmbeddingRequest(
    int Numero,
    string Nombre,
    int Horas,
    string Contenidos,
    IReadOnlyCollection<float> Embedding);

/// <summary>Parámetros para calcular la cobertura semántica Pcont (US-10, S2-J06/J07).</summary>
public record CalcularContenidoRequest(
    IReadOnlyCollection<UnidadOrigenEmbeddingRequest> UnidadesOrigen,
    IReadOnlyCollection<UnidadDestinoEmbeddingRequest> UnidadesDestino,
    double Tau,
    string RutaNormativa,
    bool ValidarRN19 = true);

/// <summary>Cobertura de una unidad de destino.</summary>
public record UnidadCoberturaResponse(
    int Numero,
    string Nombre,
    int Horas,
    double SimilitudMaxima,
    bool Cubierta,
    IReadOnlyCollection<int> UnidadesOrigenContribuyentes);

/// <summary>Resultado del cálculo de Pcont y desglose por unidad (US-10, S2-J06/J07).</summary>
public record EvaluacionContenidoResponse(
    decimal Pcont,
    IReadOnlyCollection<UnidadCoberturaResponse> UnidadesCubiertas,
    IReadOnlyCollection<UnidadCoberturaResponse> UnidadesFaltantes,
    double TauUsado,
    string DetalleCalculo,
    bool ViolacionRN19,
    IReadOnlyCollection<int> UnidadesOrigenDuplicadas);

/// <summary>Parámetros para buscar precedentes (US-11, S2-J08).</summary>
public record BuscarPrecedenteRequest(
    IReadOnlyCollection<string> CodigosAsignaturasOrigen,
    string CodigoAsignaturaDestino,
    string InstitucionOrigen);

/// <summary>Asignatura de origen en un precedente.</summary>
public record PrecedenteAsignaturaOrigenResponse(
    string Codigo,
    string Nombre);

/// <summary>Resultado de búsqueda de precedentes (US-11, S2-J08).</summary>
public record PrecedenteResponse(
    Guid Id,
    string Resolucion,
    IReadOnlyCollection<PrecedenteAsignaturaOrigenResponse> AsignaturasOrigen,
    string AsignaturaDestino,
    string InstitucionOrigen,
    DateOnly Fecha);

/// <summary>Parámetros para el análisis completo (US-11, S2-J09).</summary>
public record AnalizarExtraccionRequest(
    /// <summary>Años transcurridos desde que se cursó la asignatura de origen.</summary>
    int AnosTranscurridos,

    /// <summary>Número de matrícula en la malla destino (1, 2, 3...).</summary>
    int NumeroMatriculaDestino,

    /// <summary>True si los créditos son de otra institución.</summary>
    bool EsCreditoExterno,

    /// <summary>Ruta normativa (CAU-P-011, CAU-P-122).</summary>
    string RutaNormativa,

    /// <summary>Código de la asignatura de destino (catálogo).</summary>
    string CodigoAsignaturaDestino,

    /// <summary>Institución de origen del estudiante.</summary>
    string InstitucionOrigen,

    /// <summary>Créditos de la asignatura de destino (catálogo).</summary>
    int CreditosDestino,

    /// <summary>Unidades de destino con embeddings precalculados.</summary>
    IReadOnlyCollection<UnidadDestinoEmbeddingRequest> UnidadesDestino,

    /// <summary>Validar RN-19 (no duplicar origen). Default true.</summary>
    bool ValidarRN19 = true);

/// <summary>Estado final de la evaluación tras análisis completo.</summary>
public enum EstadoEvaluacionFinal
{
    /// <summary>Todo OK: sin bloqueos duros, el docente puede revisar y emitir dictamen.</summary>
    ListaParaDictamen = 0,

    /// <summary>Bloqueo duro por RN-16 (3ra matrícula): no continuar.</summary>
    BloqueoTerceraMatricula = 1,

    /// <summary>Bloqueo por RN-19 (origen duplicado en destinos cubiertos).</summary>
    BloqueoRN19 = 2,

    /// <summary>Requiere validación de conocimientos (RN-03, >10 años CAU-P-011).</summary>
    RequiereValidacionConocimientos = 3,

    /// <summary>Error en gRPC reglas: no se pudo obtener configuración vigente.</summary>
    ErrorReglasGrpc = 4,

    /// <summary>Extracción no está confirmada (pendiente operador).</summary>
    ExtraccionNoConfirmada = 5,

    /// <summary>El contenido cubierto no alcanza el mínimo vigente de la ruta normativa (Pcont &lt; θ).</summary>
    ContenidoInsuficiente = 6
}

/// <summary>Resultado consolidado del análisis completo (US-11, S2-J09).</summary>
public record AnalisisCompletoResponse(
    ExtraccionResponse Extraccion,
    EvaluacionCreditosResponse Creditos,
    EvaluacionContenidoResponse Contenido,
    IReadOnlyCollection<PrecedenteResponse> Precedentes,
    EstadoEvaluacionFinal EstadoFinal,
    IReadOnlyCollection<string> AlertasGlobales,
    string DetalleTrazabilidad);
