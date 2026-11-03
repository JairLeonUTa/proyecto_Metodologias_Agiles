using Microsoft.Extensions.DependencyInjection;
using Siha.MsC.Api.Services.Strategies;

namespace Siha.MsC.Api.Services;

/// <summary>
/// Registro centralizado de estrategias (Strategy Registry).
/// Facilita composición, testing y extensión para Sprint 3 (US-12 clasificación multicriterio).
/// </summary>
public static class StrategyRegistry
{
    private static readonly Dictionary<string, Type> _estrategias = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ReglaCreditos"] = typeof(IReglaCreditosStrategy),
        ["ReglaContenido"] = typeof(IReglaContenidoStrategy),
        ["Precedentes"] = typeof(IPrecedenteService),
        ["AgenteAnalizador"] = typeof(IAgenteAnalizadorService)
    };

    /// <summary>Registra todas las estrategias en el contenedor DI.</summary>
    public static IServiceCollection AddStrategies(this IServiceCollection services)
    {
        services.AddScoped<IReglaCreditosStrategy, ReglaCreditosStrategy>();
        services.AddScoped<IReglaContenidoStrategy, ReglaContenidoStrategy>();
        services.AddScoped<IPrecedenteService, PrecedenteService>();
        services.AddScoped<IAgenteAnalizadorService, AgenteAnalizadorService>();
        return services;
    }

    /// <summary>Obtiene el tipo de una estrategia por nombre.</summary>
    public static Type? ObtenerTipo(string nombre)
        => _estrategias.TryGetValue(nombre, out var tipo) ? tipo : null;

    /// <summary>Nombres de todas las estrategias registradas.</summary>
    public static IReadOnlyCollection<string> Nombres => _estrategias.Keys;
}