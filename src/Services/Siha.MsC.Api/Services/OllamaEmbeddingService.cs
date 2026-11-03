using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Siha.MsC.Api.Options;

namespace Siha.MsC.Api.Services;

/// <summary>
/// Cliente HTTP directo a la API de embeddings de Ollama.
///
/// Usa el endpoint POST /api/embeddings con el modelo configurado (ej. nomic-embed-text).
/// Compatible con Ollama 0.3.x. No requiere Microsoft.SemanticKernel.Connectors.Ollama (aún en alpha).
/// </summary>
public sealed class OllamaEmbeddingService : IOllamaEmbeddingService
{
    private readonly HttpClient _http;
    private readonly OllamaOptions _options;
    private readonly ILogger<OllamaEmbeddingService> _logger;

    public OllamaEmbeddingService(
        HttpClient http,
        IOptions<OllamaOptions> options,
        ILogger<OllamaEmbeddingService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;

        _http.BaseAddress = new Uri(_options.Url.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(_options.TimeoutSegundos);
    }

    public async Task<float[]?> GenerarEmbeddingAsync(string texto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            _logger.LogWarning("GenerarEmbeddingAsync llamado con texto vacío o nulo");
            return null;
        }

        try
        {
            var request = new OllamaEmbeddingRequest
            {
                Model = _options.ModeloEmbeddings,
                Prompt = texto.Trim()
            };

            _logger.LogDebug("Solicitando embedding a Ollama: modelo={Modelo}, longitudTexto={Len}",
                _options.ModeloEmbeddings, texto.Length);

            var response = await _http.PostAsJsonAsync("api/embeddings", request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Error generando embedding: {StatusCode} - {Error}", response.StatusCode, error);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(cancellationToken: cancellationToken);

            if (result?.Embedding is null || result.Embedding.Length == 0)
            {
                _logger.LogWarning("Ollama devolvió embedding vacío o nulo");
                return null;
            }

            _logger.LogDebug("Embedding generado OK: dimensiones={Dims}", result.Embedding.Length);
            return result.Embedding.Select(d => (float)d).ToArray();
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            _logger.LogError("Timeout generando embedding en Ollama ({Timeout}s)", _options.TimeoutSegundos);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error de red llamando a Ollama en {Url}", _options.Url);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error deserializando respuesta de embeddings de Ollama");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado generando embedding");
            return null;
        }
    }

    public async Task<bool> EstaDisponibleAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Verificar que el modelo de embeddings esté cargado
            var response = await _http.GetAsync($"api/tags", cancellationToken);
            if (!response.IsSuccessStatusCode) return false;

            var tags = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>(cancellationToken: cancellationToken);
            return tags?.Models?.Any(m => m.Name.StartsWith(_options.ModeloEmbeddings, StringComparison.OrdinalIgnoreCase)) == true;
        }
        catch
        {
            return false;
        }
    }

    private sealed record OllamaEmbeddingRequest
    {
        public string Model { get; init; } = string.Empty;
        public string Prompt { get; init; } = string.Empty;
    }

    private sealed record OllamaEmbeddingResponse
    {
        public double[] Embedding { get; init; } = Array.Empty<double>();
    }

    private sealed record OllamaTagsResponse
    {
        public OllamaModel[]? Models { get; init; }
    }

    private sealed record OllamaModel
    {
        public string Name { get; init; } = string.Empty;
    }
}