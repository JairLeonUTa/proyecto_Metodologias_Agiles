namespace Siha.MsC.Api.Options;

/// <summary>
/// Configuración del agente IA local (Ollama) que usa el Microservicio C (S2-S01).
/// En Docker Compose los valores llegan desde el servicio <c>ollama</c> (perfil sprint23).
/// </summary>
public class OllamaOptions
{
    public const string SectionName = "Ollama";

    /// <summary>Dirección del servidor Ollama (http://ollama:11434 en Docker, http://localhost:11434 en local).</summary>
    public string Url { get; set; } = "http://localhost:11434";

    /// <summary>Modelo de lenguaje para análisis y extracción asistida (US-10).</summary>
    public string Modelo { get; set; } = "llama3.2:3b";

    /// <summary>Modelo de embeddings para la similitud de unidades temáticas (US-10).</summary>
    public string ModeloEmbeddings { get; set; } = "nomic-embed-text";

    /// <summary>Tiempo máximo por llamada al modelo (segundos).</summary>
    public int TimeoutSegundos { get; set; } = 120;
}
