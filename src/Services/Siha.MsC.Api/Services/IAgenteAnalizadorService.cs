using Siha.MsC.Api.Contracts.Dtos;

// Usar el enum del DTO (contrato público)
using EstadoEvaluacionFinal = Siha.MsC.Api.Contracts.Dtos.EstadoEvaluacionFinal;

namespace Siha.MsC.Api.Services;

/// <summary>
/// Orquestador integral del agente analizador (US-11, S2-J09).
///
/// Secuencia:
/// 1) Extracción confirmada (ya validada por operador)
/// 2) gRPC ReglasGrpc → obtiene θ, δ, τ y reglas normativas vigentes
/// 3) ReglaCreditosStrategy → Pcred + controles (vigencia 10 años, 3ra matrícula, externos)
/// 4) ReglaContenidoStrategy → Pcont (similitud coseno) + RN-19
/// 5) Búsqueda precedentes → coincidencia exacta
///
/// Devuelve resultado consolidado para el docente.
/// </summary>
public interface IAgenteAnalizadorService
{
    /// <summary>
    /// Ejecuta el análisis completo de una extracción confirmada.
    /// </summary>
    /// <param name="extraccionId">ID de la extracción en estado CONFIRMADA.</param>
    /// <param name="parametrosAnalisis">Parámetros adicionales: años, matrícula, externo, ruta normativa, etc.</    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Resultado consolidado del análisis.</returns>
    Task<ResultadoAnalisisCompleto> AnalizarAsync(
        Guid extraccionId,
        ParametrosAnalisisCompleto parametrosAnalisis,
        CancellationToken cancellationToken = default);
}

/// <summary>Parámetros que el docente/operador provee para el análisis (no vienen en la extracción).</summary>
public sealed record ParametrosAnalisisCompleto(
    /// <summary>Años transcurridos desde que se cursó la asignatura de origen.</summary>
    int AnosTranscurridos,

    /// <summary>Número de matrícula en la malla destino (1, 2, 3...).</summary>
    int NumeroMatriculaDestino,

    /// <summary>True si los créditos son de otra institución (extranjeros).</summary>
    bool EsCreditoExterno,

    /// <summary>Ruta normativa aplicable (CAU-P-011 = cambio carrera, CAU-P-122 = movilidad).</summary>
    string RutaNormativa,

    /// <summary>Código de la asignatura de destino (del catálogo) para buscar precedentes.</summary>
    string CodigoAsignaturaDestino,

    /// <summary>Institución de origen del estudiante.</summary>
    string InstitucionOrigen,

    /// <summary>Creditos de la asignatura de destino (del catálogo).</summary>
    int CreditosDestino,

    /// <summary>Unidades de destino con embeddings precalculados (del catálogo).</summary>
    IReadOnlyCollection<UnidadDestinoEmbeddingRequest> UnidadesDestino,

    /// <summary>Si true, valida RN-19 (no duplicar origen). Default true.</summary>
    bool ValidarRN19 = true
);

/// <summary>Resultado consolidado del análisis completo (S2-J09).</summary>
public sealed record ResultadoAnalisisCompleto(
    /// <summary>Datos de la extracción confirmada.</summary>
    ExtraccionResponse Extraccion,

    /// <summary>Resultado de créditos (Pcred + controles normativos).</summary>
    EvaluacionCreditosResponse Creditos,

    /// <summary>Resultado de contenidos (Pcont + desglose + RN-19).</summary>
    EvaluacionContenidoResponse Contenido,

    /// <summary>Precedentes encontrados (coincidencia exacta).</summary>
    IReadOnlyCollection<PrecedenteResponse> Precedentes,

    /// <summary>Estado final de la evaluación para el flujo.</summary>
    EstadoEvaluacionFinal EstadoFinal,

    /// <summary>Mensajes/alertas globales para el docente.</summary>
    IReadOnlyCollection<string> AlertasGlobales,

    /// <summary>Detalle de trazabilidad completa (qué reglas/versiones se aplicaron).</summary>
    string DetalleTrazabilidad
);

