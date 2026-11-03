using Siha.MsC.Api.Domain;

namespace Siha.MsC.Api.Services;

/// <summary>Interfaz para la estrategia de similitud semántica de contenidos (US-10, S2-J06).</summary>
public interface IReglaContenidoStrategy
{
    /// <summary>
    /// Calcula la cobertura temática (Pcont) comparando embeddings de unidades origen vs destino.
    /// </summary>
    /// <param name="parametros">Unidades origen/destino con embeddings, umbral τ.</param>
    /// <returns>Pcont, desglose por unidad (cubiertas/faltantes), trazabilidad.</returns>
    EvaluacionContenido Calcular(ParametrosContenido parametros);
}