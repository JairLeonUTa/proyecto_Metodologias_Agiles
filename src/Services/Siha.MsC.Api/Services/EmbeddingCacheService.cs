using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Siha.MsC.Api.Data;
using Siha.MsC.Api.Domain.Entities;
using Siha.MsC.Api.Options;

namespace Siha.MsC.Api.Services;

/// <summary>Unidad de un plan analítico cuyo embedding se quiere tener en caché.</summary>
public sealed record UnidadPlanEmbedding(string CodigoAsignatura, int NumeroUnidad, string Contenidos);

/// <summary>Resumen de una precarga de embeddings (S2-S05).</summary>
public sealed record ResultadoPrecalculoEmbeddings(int Total, int DesdeCache, int Generados, int Fallidos);

/// <summary>
/// Caché persistente de embeddings de unidades en DB C (tabla EMBEDDING_UNIDAD, S2-S05 / US-10).
/// </summary>
public interface IEmbeddingCacheService
{
    /// <summary>
    /// Devuelve el vector de la unidad. Si hay una fila con el mismo modelo y el mismo SHA-256 del texto,
    /// se usa sin llamar a Ollama; si no, se genera y se guarda. Si Ollama no responde devuelve null
    /// y no guarda nada (nunca se inventa un vector).
    /// </summary>
    Task<float[]?> ObtenerAsync(string codigoAsignatura, int numeroUnidad, string contenidos, CancellationToken cancellationToken = default);

    /// <summary>Precarga los vectores de un conjunto de unidades (por ejemplo, los 43 planes de Software).</summary>
    Task<ResultadoPrecalculoEmbeddings> PrecalcularAsync(IEnumerable<UnidadPlanEmbedding> unidades, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class EmbeddingCacheService : IEmbeddingCacheService
{
    private readonly SihaDbContextC _db;
    private readonly IOllamaEmbeddingService _ollama;
    private readonly OllamaOptions _options;
    private readonly ILogger<EmbeddingCacheService> _logger;

    public EmbeddingCacheService(
        SihaDbContextC db,
        IOllamaEmbeddingService ollama,
        IOptions<OllamaOptions> options,
        ILogger<EmbeddingCacheService> logger)
    {
        _db = db;
        _ollama = ollama;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<float[]?> ObtenerAsync(
        string codigoAsignatura, int numeroUnidad, string contenidos, CancellationToken cancellationToken = default)
    {
        var (vector, _) = await ObtenerConOrigenAsync(codigoAsignatura, numeroUnidad, contenidos, cancellationToken);
        return vector;
    }

    public async Task<ResultadoPrecalculoEmbeddings> PrecalcularAsync(
        IEnumerable<UnidadPlanEmbedding> unidades, CancellationToken cancellationToken = default)
    {
        int total = 0, cache = 0, generados = 0, fallidos = 0;
        foreach (var u in unidades)
        {
            total++;
            var (vector, desdeCache) = await ObtenerConOrigenAsync(u.CodigoAsignatura, u.NumeroUnidad, u.Contenidos, cancellationToken);
            if (vector is null) fallidos++;
            else if (desdeCache) cache++;
            else generados++;
        }

        _logger.LogInformation(
            "Precarga de embeddings: total={Total} cache={Cache} generados={Generados} fallidos={Fallidos}",
            total, cache, generados, fallidos);
        return new ResultadoPrecalculoEmbeddings(total, cache, generados, fallidos);
    }

    private async Task<(float[]? Vector, bool DesdeCache)> ObtenerConOrigenAsync(
        string codigoAsignatura, int numeroUnidad, string contenidos, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(codigoAsignatura))
            throw new ArgumentException("El código de asignatura es obligatorio.", nameof(codigoAsignatura));
        if (string.IsNullOrWhiteSpace(contenidos))
            return (null, false);

        var codigo = codigoAsignatura.Trim().ToUpperInvariant();
        var modelo = _options.ModeloEmbeddings;
        var texto = contenidos.Trim();
        var hash = Sha256(texto);

        var fila = await _db.EmbeddingsUnidad.FirstOrDefaultAsync(
            e => e.CodigoAsignatura == codigo && e.NumeroUnidad == numeroUnidad && e.Modelo == modelo,
            cancellationToken);

        if (fila is not null && fila.HashContenido == hash && fila.Dimensiones > 0)
            return (DesdeBytes(fila.Vector), true);

        var vector = await _ollama.GenerarEmbeddingAsync(texto, cancellationToken);
        if (vector is null || vector.Length == 0)
        {
            _logger.LogWarning("Sin embedding para {Codigo} U{Numero}: Ollama no respondió; no se guarda nada.", codigo, numeroUnidad);
            return (null, false);
        }

        if (fila is null)
        {
            fila = new EmbeddingUnidad { CodigoAsignatura = codigo, NumeroUnidad = numeroUnidad, Modelo = modelo };
            _db.EmbeddingsUnidad.Add(fila);
        }

        // Si el texto del plan cambió, se reemplaza el vector anterior.
        fila.HashContenido = hash;
        fila.Dimensiones = vector.Length;
        fila.Vector = ABytes(vector);
        fila.ActualizadoUtc = DateTimeOffset.UtcNow;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Otra instancia guardó la misma unidad a la vez (índice único): su vector es igual de válido.
            _logger.LogInformation(ex, "Embedding de {Codigo} U{Numero} ya guardado por otra petición.", codigo, numeroUnidad);
            _db.Entry(fila).State = EntityState.Detached;
        }

        return (vector, false);
    }

    internal static string Sha256(string texto) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto))).ToLowerInvariant();

    internal static byte[] ABytes(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        for (var i = 0; i < vector.Length; i++)
            System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * sizeof(float)), vector[i]);
        return bytes;
    }

    internal static float[] DesdeBytes(byte[] bytes)
    {
        var vector = new float[bytes.Length / sizeof(float)];
        for (var i = 0; i < vector.Length; i++)
            vector[i] = System.Buffers.Binary.BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(i * sizeof(float)));
        return vector;
    }
}
