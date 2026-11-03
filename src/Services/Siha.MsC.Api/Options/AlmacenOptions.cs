namespace Siha.MsC.Api.Options;

/// <summary>Configuración del volumen compartido de documentos (mismo volumen que el Microservicio B).</summary>
public class AlmacenOptions
{
    public const string SectionName = "Archivos";

    /// <summary>Directorio raíz de los PDF. En Docker es /app/archivos.</summary>
    public string RutaAlmacenamiento { get; set; } = "archivos";
}
