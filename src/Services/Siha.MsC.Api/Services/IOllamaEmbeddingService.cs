namespace Siha.MsC.Api.Services;

/// <summary>Servicio para generar embeddings vectoriales vía Ollama (US-10, S2-J05).</summary>
public interface IOllamaEmbeddingService
{
    /// <summary>
    /// Genera el embedding vectorial de un texto usando el modelo configurado (ej. nomic-embed-text).
    /// </summary>
    /// <param name="texto">Texto a vectorizar (descripción de unidad, contenido temático, etc.).</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Vector de embeddings (float[]), o null si el servicio no está disponible.</returns>
    Task<float[]?> GenerarEmbeddingAsync(string texto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica si el servicio Ollama está disponible y el modelo de embeddings cargado.
    /// </summary>
    Task<bool> EstaDisponibleAsync(CancellationToken cancellationToken = default);
}