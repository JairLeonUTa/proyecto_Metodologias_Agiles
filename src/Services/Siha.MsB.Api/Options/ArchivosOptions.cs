namespace Siha.MsB.Api.Options;

/// <summary>
/// Configuración del módulo de archivos del Microservicio B (S1-S05 / S1-S06).
/// Se enlaza desde la sección <c>Archivos</c> de appsettings / variables de entorno
/// (p. ej. <c>Archivos__TamanoMaximoBytes</c> y <c>Archivos__RutaAlmacenamiento</c>).
/// </summary>
public sealed class ArchivosOptions
{
    public const string SectionName = "Archivos";

    /// <summary>Tamaño máximo admitido por documento PDF, en bytes (por defecto 10 MiB).</summary>
    public long TamanoMaximoBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>Extensión obligatoria de los documentos admitidos.</summary>
    public string ExtensionPermitida { get; set; } = ".pdf";

    /// <summary>
    /// Directorio raíz donde se almacenan los documentos. Puede ser absoluto o relativo al
    /// directorio de trabajo del servicio. Nunca se usa el nombre enviado por el cliente.
    /// </summary>
    public string RutaAlmacenamiento { get; set; } = "archivos";

    /// <summary>Tipos MIME aceptados para un PDF (el cliente puede enviar variantes).</summary>
    public string[] MimePermitidos { get; set; } = { "application/pdf", "application/x-pdf" };
}
