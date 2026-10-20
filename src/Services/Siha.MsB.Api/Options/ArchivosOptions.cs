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

    /// <summary>
    /// Requisito documental mínimo para enviar el expediente: cantidad de PDF en estado VALIDADO,
    /// presentes en disco y con hash íntegro (por defecto 1).
    /// </summary>
    public int MinimoDocumentosRequeridos { get; set; } = 1;
}

/// <summary>
/// Reintento automático de publicaciones pendientes (sección <c>Republicacion</c>). Recupera los
/// expedientes que quedaron en REGISTRADO_PENDIENTE_PUBLICACION por una caída del bus, o atascados en
/// PUBLICACION_EN_CURSO por un reinicio del proceso, conservando siempre su CorrelationId.
/// </summary>
public sealed class RepublicacionOptions
{
    public const string SectionName = "Republicacion";

    public bool Habilitada { get; set; } = true;

    /// <summary>Intervalo entre barridos, en segundos.</summary>
    public int IntervaloSegundos { get; set; } = 30;

    /// <summary>Antigüedad a partir de la cual un PUBLICACION_EN_CURSO se considera abandonado.</summary>
    public int EnCursoAbandonadoSegundos { get; set; } = 120;

    /// <summary>Máximo de expedientes procesados por barrido.</summary>
    public int LoteMaximo { get; set; } = 20;
}
