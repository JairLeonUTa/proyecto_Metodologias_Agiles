namespace Siha.MsC.Api.Services.Strategies;

/// <summary>
/// Interfaz base unificada para todas las estrategias de evaluación (Strategy Pattern consolidado).
/// Permite composición, testing y extensión uniforme (S2-J10, preparación Sprint 3 US-12/US-13).
/// </summary>
/// <typeparam name="TInput">Parámetros de entrada tipados.</typeparam>
/// <typeparam name="TOutput">Resultado tipado.</typeparam>
public interface IStrategy<TInput, TOutput>
{
    /// <summary>Nombre identificador de la estrategia (para logging/trazabilidad).</summary>
    string Nombre { get; }

    /// <summary>Versión de la estrategia (para trazabilidad en auditoría).</summary>
    string Version { get; }

    /// <summary>
    /// Ejecuta la lógica de la estrategia.
    /// </summary>
    /// <param name="input">Parámetros de entrada validados.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Resultado tipado con trazabilidad.</param>
    Task<TOutput> EjecutarAsync(TInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validación rápida de precondiciones (sin efectos secundarios).
    /// </summary>
    /// <param name="input">Parámetros a validar.</param>
    /// <returns>Null si válido, mensaje de error si inválido.</param>
    string? ValidarPrecondiciones(TInput input);
}

/// <summary>
/// Resultado base con trazabilidad para todas las estrategias.</summary>
public abstract record StrategyResultadoBase
{
    /// <summary>Nombre de la estrategia que generó el resultado.</summary>
    public string Estrategia { get; init; } = string.Empty;

    /// <summary>Versión de la estrategia.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>Timestamp UTC de la ejecución.</summary>
    public DateTimeOffset EjecutadoUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Detalle de trazabilidad (parámetros, reglas aplicadas, etc.).</summary>
    public string DetalleTrazabilidad { get; init; } = string.Empty;

    /// <summary>Alertas/advertencias generadas durante la ejecución.</summary>
    public IReadOnlyCollection<string> Alertas { get; init; } = Array.Empty<string>();
}