using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Siha.MsB.Api.Options;

namespace Siha.MsB.Api.Services;

/// <summary>Resultado de la validación de un archivo PDF (S1-S05).</summary>
public sealed record ResultadoValidacionPdf(bool Valido, string? Error)
{
    public static ResultadoValidacionPdf Ok() => new(true, null);

    public static ResultadoValidacionPdf Fallo(string error) => new(false, error);
}

/// <summary>Metadatos de un documento ya almacenado de forma segura en disco (S1-S06).</summary>
public sealed record DocumentoAlmacenado(
    string NombreOriginal,
    string NombreAlmacenado,
    string RutaAlmacenamiento,
    string RutaFisica,
    string HashSha256,
    long TamanoBytes,
    string MimeType);

/// <summary>
/// Validación y almacenamiento seguro de los documentos PDF de un expediente (S1-S05 / S1-S06).
/// La validación es independiente del almacenamiento para poder probarse por separado.
/// </summary>
public interface IArchivoStorageService
{
    /// <summary>Tamaño máximo admitido, en bytes (expuesto para las respuestas de error).</summary>
    long TamanoMaximoBytes { get; }

    /// <summary>
    /// Valida el archivo recibido: no vacío, dentro del tamaño máximo, extensión y MIME correctos
    /// y firma binaria <c>%PDF</c> en los primeros bytes del contenido.
    /// </summary>
    ResultadoValidacionPdf ValidarPdf(IFormFile? archivo);

    /// <summary>Calcula el hash SHA-256 (hexadecimal en minúsculas) del contenido indicado.</summary>
    string CalcularHashSha256(Stream contenido);

    /// <summary>
    /// Copia el archivo a la ruta configurada con un nombre generado (sin usar el nombre del
    /// cliente) y devuelve sus metadatos, incluida la ruta relativa y el hash SHA-256.
    /// </summary>
    Task<DocumentoAlmacenado> GuardarAsync(IFormFile archivo, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementación de <see cref="IArchivoStorageService"/>.
///
/// Reglas de seguridad aplicadas:
///  - El nombre en disco se genera con un GUID: el nombre enviado por el cliente nunca toca el sistema de archivos.
///  - La ruta final se comprueba contra el directorio raíz configurado (defensa ante path traversal).
///  - Se calcula siempre el hash SHA-256 del contenido almacenado (integridad y deduplicación).
/// </summary>
public class ArchivoStorageService : IArchivoStorageService
{
    /// <summary>Firma binaria obligatoria de un PDF.</summary>
    private static readonly byte[] FirmaPdf = "%PDF"u8.ToArray();

    private readonly ArchivosOptions _opciones;
    private readonly ILogger<ArchivoStorageService> _logger;

    public ArchivoStorageService(IOptions<ArchivosOptions> opciones, ILogger<ArchivoStorageService> logger)
    {
        _opciones = opciones.Value;
        _logger = logger;
    }

    public long TamanoMaximoBytes => _opciones.TamanoMaximoBytes;

    public ResultadoValidacionPdf ValidarPdf(IFormFile? archivo)
    {
        if (archivo is null)
        {
            return ResultadoValidacionPdf.Fallo("Debe adjuntar un archivo PDF en el campo 'archivo'.");
        }

        var nombre = archivo.FileName ?? string.Empty;

        // 1) Archivos vacíos.
        if (archivo.Length <= 0)
        {
            return ResultadoValidacionPdf.Fallo("El archivo está vacío (0 bytes).");
        }

        // 2) Tamaño máximo configurable.
        if (archivo.Length > _opciones.TamanoMaximoBytes)
        {
            return ResultadoValidacionPdf.Fallo(
                $"El archivo supera el tamaño máximo permitido de {_opciones.TamanoMaximoBytes} bytes.");
        }

        // 3) Extensión.
        var extension = Path.GetExtension(nombre);
        if (!string.Equals(extension, _opciones.ExtensionPermitida, StringComparison.OrdinalIgnoreCase))
        {
            return ResultadoValidacionPdf.Fallo(
                $"Solo se admiten documentos con extensión '{_opciones.ExtensionPermitida}' (recibido '{nombre}').");
        }

        // 4) Tipo MIME declarado por el cliente.
        var mime = NormalizarMime(archivo.ContentType);
        if (!_opciones.MimePermitidos.Contains(mime, StringComparer.OrdinalIgnoreCase))
        {
            return ResultadoValidacionPdf.Fallo(
                $"El tipo MIME '{archivo.ContentType}' no corresponde a un PDF. " +
                $"Valores permitidos: {string.Join(", ", _opciones.MimePermitidos)}.");
        }

        // 5) Firma binaria: el contenido real debe empezar por "%PDF".
        if (!TieneFirmaPdf(archivo))
        {
            return ResultadoValidacionPdf.Fallo(
                "El contenido del archivo no corresponde a un PDF válido (falta la firma binaria %PDF).");
        }

        return ResultadoValidacionPdf.Ok();
    }

    public string CalcularHashSha256(Stream contenido)
        => Convert.ToHexString(SHA256.HashData(contenido)).ToLowerInvariant();

    public async Task<DocumentoAlmacenado> GuardarAsync(
        IFormFile archivo,
        CancellationToken cancellationToken = default)
    {
        var ahora = DateTimeOffset.UtcNow;
        var raiz = Path.GetFullPath(_opciones.RutaAlmacenamiento);
        var subcarpeta = Path.Combine(ahora.Year.ToString(), ahora.Month.ToString("00"));
        var directorio = Path.Combine(raiz, subcarpeta);
        Directory.CreateDirectory(directorio);

        // Nombre generado: nunca se reutiliza el nombre enviado por el cliente.
        var nombreAlmacenado = $"{Guid.NewGuid():N}{_opciones.ExtensionPermitida}";
        var rutaFisica = Path.GetFullPath(Path.Combine(directorio, nombreAlmacenado));

        // Defensa ante path traversal: la ruta final debe quedar dentro del directorio configurado.
        if (!rutaFisica.StartsWith(raiz, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("La ruta de almacenamiento calculada es inválida.");
        }

        await using (var origen = archivo.OpenReadStream())
        await using (var destino = File.Create(rutaFisica))
        {
            await origen.CopyToAsync(destino, cancellationToken);
        }

        string hash;
        await using (var contenido = archivo.OpenReadStream())
        {
            hash = Convert.ToHexString(await SHA256.HashDataAsync(contenido, cancellationToken)).ToLowerInvariant();
        }

        var rutaRelativa = Path.Combine(subcarpeta, nombreAlmacenado).Replace('\\', '/');

        _logger.LogInformation(
            "Documento almacenado. NombreOriginal={NombreOriginal} NombreAlmacenado={NombreAlmacenado} TamanoBytes={TamanoBytes} Hash={Hash}",
            archivo.FileName, nombreAlmacenado, archivo.Length, hash);

        return new DocumentoAlmacenado(
            archivo.FileName ?? nombreAlmacenado,
            nombreAlmacenado,
            rutaRelativa,
            rutaFisica,
            hash,
            archivo.Length,
            NormalizarMime(archivo.ContentType));
    }

    private static string NormalizarMime(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return string.Empty;
        }

        var indice = contentType.IndexOf(';');
        return (indice >= 0 ? contentType[..indice] : contentType).Trim().ToLowerInvariant();
    }

    private static bool TieneFirmaPdf(IFormFile archivo)
    {
        using var stream = archivo.OpenReadStream();

        var cabecera = new byte[FirmaPdf.Length];
        var leidos = 0;

        while (leidos < cabecera.Length)
        {
            var n = stream.Read(cabecera, leidos, cabecera.Length - leidos);
            if (n == 0)
            {
                break;
            }

            leidos += n;
        }

        return leidos == FirmaPdf.Length && cabecera.AsSpan().SequenceEqual(FirmaPdf);
    }
}
