using Siha.MsC.Api.Domain;

namespace Siha.MsC.Api.Services;

/// <summary>Interfaz para la estrategia de cálculo de proporción de créditos y controles normativos (US-09).</summary>
public interface IReglaCreditosStrategy
{
    /// <summary>
    /// Calcula Pcred y evalúa controles normativos (vigencia 10 años, 3ra matrícula, créditos externos).
    /// </summary>
    /// <param name="parametros">Datos de entrada: créditos origen/destino, años, matrícula, tipo, ruta.</param>
    /// <returns>Resultado con Pcred, categoría, alertas y posible bloqueo duro.</returns>
    EvaluacionCreditos Calcular(ParametrosCreditos parametros);
}