namespace Siha.MsC.Api.Domain.Entities;

/// <summary>
/// Caché del embedding de una unidad temática (tabla EMBEDDING_UNIDAD, DB C, S2-S05 / US-10).
/// Evita volver a pedir a Ollama el vector de las unidades de los planes analíticos de Software
/// en cada análisis. La fila solo se reutiliza si el modelo y el SHA-256 del texto coinciden:
/// si el plan cambia en el catálogo, el vector se recalcula.
/// </summary>
public class EmbeddingUnidad
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Código de la asignatura dueña de la unidad (ej. SW201).</summary>
    public string CodigoAsignatura { get; set; } = string.Empty;

    /// <summary>Número de la unidad dentro del plan analítico.</summary>
    public int NumeroUnidad { get; set; }

    /// <summary>Modelo de embeddings con el que se generó el vector (ej. nomic-embed-text).</summary>
    public string Modelo { get; set; } = string.Empty;

    /// <summary>SHA-256 en hexadecimal del texto normalizado que se envió al modelo.</summary>
    public string HashContenido { get; set; } = string.Empty;

    public int Dimensiones { get; set; }

    /// <summary>Vector float32 en little-endian (4 bytes por dimensión).</summary>
    public byte[] Vector { get; set; } = Array.Empty<byte>();

    public DateTimeOffset CreadoUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ActualizadoUtc { get; set; } = DateTimeOffset.UtcNow;
}
