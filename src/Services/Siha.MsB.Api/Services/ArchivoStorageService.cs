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

    /// <summary>Calcula en el servidor el SHA-256 del archivo recibido (sin almacenarlo).</summary>
    Task<string> CalcularHashAsync(IFormFile archivo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Comprueba, desde el disco, que el archivo referenciado existe, conserva el tamaño registrado y
    /// su SHA-256 coincide con el persistido. Es la verificación previa a publicar en el bus.
    /// </summary>
    Task<ResultadoVerificacionArchivo> VerificarAsync(
        string rutaRelativa,
        string hashEsperado,
        long tamanoEsperado,
        CancellationToken cancellationToken = default);

    /// <summary>Elimina el archivo físico si existe (compensación o retiro de un documento).</summary>
    void Eliminar(string rutaRelativa);
}

/// <summary>Resultado de verificar en disco un documento ya almacenado.</summary>
public sealed record ResultadoVerificacionArchivo(bool Disponible, string? Motivo, string? HashCalculado)
{
    public static ResultadoVerificacionArchivo Ok(string hash) => new(true, null, hash);

    public static ResultadoVerificacionArchivo Fallo(string motivo, string? hash = null) => new(false, motivo, hash);
}

/// <summary>
/// Fallo del almacenamiento físico (disco lleno, permisos, volumen no montado...). El controlador lo
/// traduce a 503 sin dejar filas ni archivos huérfanos, de modo que el cliente puede reintentar.
/// </summary>
public sealed class AlmacenamientoNoDisponibleException : Exception
{
    public AlmacenamientoNoDisponibleException(string mensaje, Exception interna) : base(mensaje, interna)
    {
    }
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
        var raiz = RaizAlmacenamiento();
        var subcarpeta = Path.Combine(ahora.Year.ToString(), ahora.Month.ToString("00"));
        var directorio = Path.Combine(raiz, subcarpeta);

        // Nombre generado: nunca se reutiliza el nombre enviado por el cliente.
        var nombreAlmacenado = $"{Guid.NewGuid():N}{_opciones.ExtensionPermitida}";
        var rutaFisica = Path.GetFullPath(Path.Combine(directorio, nombreAlmacenado));
        var rutaTemporal = rutaFisica + ".tmp";

        // Defensa ante path traversal: la ruta final debe quedar dentro del directorio configurado.
        if (!EstaDentroDeRaiz(rutaFisica, raiz))
        {
            throw new InvalidOperationException("La ruta de almacenamiento calculada es inválida.");
        }

        string hash;
        long escritos;

        try
        {
            Directory.CreateDirectory(directorio);

            // Escritura atómica: se escribe en un .tmp, se fuerza a disco y solo entonces se renombra.
            // El hash se calcula sobre los mismos bytes que se escriben (fuente de verdad: el servidor).
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var origen = archivo.OpenReadStream())
            await using (var destino = new FileStream(
                rutaTemporal, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                int leidos;
                escritos = 0;

                while ((leidos = await origen.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    sha.AppendData(buffer, 0, leidos);
                    await destino.WriteAsync(buffer.AsMemory(0, leidos), cancellationToken);
                    escritos += leidos;
                }

                await destino.FlushAsync(cancellationToken);
                destino.Flush(flushToDisk: true);
            }

            hash = Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant();
            File.Move(rutaTemporal, rutaFisica);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            BorrarSilencioso(rutaTemporal);
            BorrarSilencioso(rutaFisica);
            _logger.LogError(ex, "No se pudo escribir el documento en {Directorio}.", directorio);
            throw new AlmacenamientoNoDisponibleException(
                "El almacenamiento de documentos no está disponible en este momento. Intente nuevamente.", ex);
        }
        catch
        {
            BorrarSilencioso(rutaTemporal);
            BorrarSilencioso(rutaFisica);
            throw;
        }

        var rutaRelativa = Path.Combine(subcarpeta, nombreAlmacenado).Replace('\\', '/');

        _logger.LogInformation(
            "Documento almacenado. NombreOriginal={NombreOriginal} NombreAlmacenado={NombreAlmacenado} TamanoBytes={TamanoBytes} Hash={Hash}",
            archivo.FileName, nombreAlmacenado, escritos, hash);

        return new DocumentoAlmacenado(
            archivo.FileName ?? nombreAlmacenado,
            nombreAlmacenado,
            rutaRelativa,
            rutaFisica,
            hash,
            escritos,
            NormalizarMime(archivo.ContentType));
    }

    public async Task<string> CalcularHashAsync(IFormFile archivo, CancellationToken cancellationToken = default)
    {
        await using var contenido = archivo.OpenReadStream();
        return Convert.ToHexString(await SHA256.HashDataAsync(contenido, cancellationToken)).ToLowerInvariant();
    }

    public async Task<ResultadoVerificacionArchivo> VerificarAsync(
        string rutaRelativa,
        string hashEsperado,
        long tamanoEsperado,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rutaRelativa) || string.IsNullOrWhiteSpace(hashEsperado))
        {
            return ResultadoVerificacionArchivo.Fallo("El documento no tiene ruta o hash registrados por el servidor.");
        }

        string rutaFisica;
        try
        {
            rutaFisica = ResolverRutaFisica(rutaRelativa);
        }
        catch (InvalidOperationException ex)
        {
            return ResultadoVerificacionArchivo.Fallo(ex.Message);
        }

        try
        {
            var info = new FileInfo(rutaFisica);
            if (!info.Exists)
            {
                return ResultadoVerificacionArchivo.Fallo("El archivo no existe en el almacenamiento.");
            }

            if (info.Length != tamanoEsperado)
            {
                return ResultadoVerificacionArchivo.Fallo(
                    $"El tamaño en disco ({info.Length} bytes) no coincide con el registrado ({tamanoEsperado} bytes).");
            }

            await using var contenido = new FileStream(
                rutaFisica, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(contenido, cancellationToken)).ToLowerInvariant();

            return string.Equals(hash, hashEsperado, StringComparison.OrdinalIgnoreCase)
                ? ResultadoVerificacionArchivo.Ok(hash)
                : ResultadoVerificacionArchivo.Fallo("El SHA-256 recalculado desde disco no coincide con el registrado.", hash);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ResultadoVerificacionArchivo.Fallo($"No se pudo leer el archivo: {ex.Message}");
        }
    }

    public void Eliminar(string rutaRelativa)
    {
        if (string.IsNullOrWhiteSpace(rutaRelativa))
        {
            return;
        }

        try
        {
            BorrarSilencioso(ResolverRutaFisica(rutaRelativa));
        }
        catch (InvalidOperationException)
        {
            // Ruta fuera de la raíz: no se toca nada.
        }
    }

    private string RaizAlmacenamiento() => Path.GetFullPath(_opciones.RutaAlmacenamiento);

    private string ResolverRutaFisica(string rutaRelativa)
    {
        var raiz = RaizAlmacenamiento();
        var rutaFisica = Path.GetFullPath(Path.Combine(raiz, rutaRelativa));

        if (!EstaDentroDeRaiz(rutaFisica, raiz))
        {
            throw new InvalidOperationException("La ruta del documento queda fuera del almacenamiento configurado.");
        }

        return rutaFisica;
    }

    private static bool EstaDentroDeRaiz(string rutaFisica, string raiz)
    {
        var raizConSeparador = raiz.EndsWith(Path.DirectorySeparatorChar) ? raiz : raiz + Path.DirectorySeparatorChar;
        return rutaFisica.StartsWith(raizConSeparador, StringComparison.OrdinalIgnoreCase);
    }

    private void BorrarSilencioso(string ruta)
    {
        try
        {
            if (File.Exists(ruta))
            {
                File.Delete(ruta);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "No se pudo eliminar el archivo {Ruta}.", ruta);
        }
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
