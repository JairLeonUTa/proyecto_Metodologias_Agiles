namespace Siha.MsC.Api.Options;

/// <summary>Configuración del cliente gRPC hacia el Microservicio A (ReglasGrpc).</summary>
public class ReglasGrpcOptions
{
    public const string SectionName = "ReglasGrpc";

    /// <summary>Dirección del servidor gRPC (ej. http://ms-a:8081 en Docker, http://localhost:5083 en local).</summary>
    public string Url { get; set; } = "http://localhost:5083";

    /// <summary>Tiempo de espera por llamada (segundos).</summary>
    public int TimeoutSegundos { get; set; } = 10;

    /// <summary>Reintentos ante fallos transitorios.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Intervalo base entre reintentos (segundos).</summary>
    public int RetryBaseDelaySegundos { get; set; } = 2;
}