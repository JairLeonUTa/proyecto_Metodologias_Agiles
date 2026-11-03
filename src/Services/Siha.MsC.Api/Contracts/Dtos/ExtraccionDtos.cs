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
