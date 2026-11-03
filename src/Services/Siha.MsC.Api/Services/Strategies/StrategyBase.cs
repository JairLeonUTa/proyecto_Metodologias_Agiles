using System.Text.Json;
using Siha.MsC.Api.Contracts.Dtos;

namespace Siha.MsC.Api.Services.Strategies;

/// <summary>
/// Implementación base del patrón Strategy con trazabilidad, validación y manejo de errores unificado.
/// Las estrategias concretas (ReglaCreditos, ReglaContenido, etc.) heredan de aquí.
/// </summary>
/// <typeparam name="TInput">Parámetros de entrada.</typeparam>
/// <typeparam name="TOutput">Resultado tipado.</typeparam>
public abstract class StrategyBase<TInput, TOutput> : IStrategy<TInput, TOutput>
    where TOutput : StrategyResultadoBase
{
    private readonly ILogger _logger;

    protected StrategyBase(ILogger logger)
    {
        _logger = logger;
    }

    public abstract string Nombre { get; }
    public abstract string Version { get; }

    public async Task<TOutput> EjecutarAsync(TInput input, CancellationToken cancellationToken = default)
    {
        var inicio = DateTimeOffset.UtcNow;
        var alertas = new List<string>();
        var trazabilidad = new List<string>();

        try
        {
            // 1. Validar precondiciones
            var errorValidacion = ValidarPrecondiciones(input);
            if (errorValidacion is not null)
            {
                return CrearResultadoError($"Precondición fallida: {errorValidacion}", alertas, trazabilidad);
            }

            trazabilidad.Add($"Iniciando {Nombre} v{Version}");

            // 2. Ejecutar lógica concreta
            var resultado = await EjecutarCoreAsync(input, alertas, trazabilidad, cancellationToken);

            // 3. Enriquecer con metadatos comunes - crear nueva instancia con with-expression (records)
            var resultadoFinal = resultado with
            {
                Estrategia = Nombre,
                Version = Version,
                EjecutadoUtc = DateTimeOffset.UtcNow,
                DetalleTrazabilidad = string.Join(" | ", trazabilidad),
                Alertas = alertas.Distinct().ToList().AsReadOnly()
            };

            _logger.LogInformation("{Estrategia} completada en {Duracion}ms. Alertas: {Alertas}",
                Nombre, (DateTimeOffset.UtcNow - inicio).TotalMilliseconds, resultadoFinal.Alertas.Count);

            return resultadoFinal;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ejecutando {Estrategia}", Nombre);
            return CrearResultadoError($"Error interno: {ex.Message}", alertas, trazabilidad);
        }
    }

    /// <summary>Validación de precondiciones (sobrescribir en clase concreta).</summary>
    public virtual string? ValidarPrecondiciones(TInput input) => null;

    /// <summary>Lógica principal de la estrategia (implementar en clase concreta).</summary>
    protected abstract Task<TOutput> EjecutarCoreAsync(
        TInput input,
        List<string> alertas,
        List<string> trazabilidad,
        CancellationToken cancellationToken);

    /// <summary>Crea resultado de error tipado.</summary>
    protected abstract TOutput CrearResultadoError(string mensaje, List<string> alertas, List<string> trazabilidad);
}