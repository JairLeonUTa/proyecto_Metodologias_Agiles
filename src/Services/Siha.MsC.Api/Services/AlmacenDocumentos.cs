using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Siha.Contracts.Events;
using Siha.MsC.Api.Options;

namespace Siha.MsC.Api.Services;

public sealed record VerificacionDocumento(bool Disponible, string? Motivo);

public interface IAlmacenDocumentos
{
    /// <summary>
    /// Comprueba que la referencia del evento apunta a un PDF dentro del volumen compartido,
    /// con el tamaño y el SHA-256 calculados por el Microservicio B.
    /// </summary>
    Task<VerificacionDocumento> VerificarAsync(DocumentoReferencia documento, CancellationToken cancellationToken = default);
}

/// <summary>
/// Lee los PDF desde el volumen compartido con DB B. No confía en la ruta del evento:
/// rechaza rutas absolutas, traversal, enlaces simbólicos y hashes/tamaños alterados.
/// </summary>
public class AlmacenDocumentos : IAlmacenDocumentos
{
    private readonly string _raiz;

    public AlmacenDocumentos(IOptions<AlmacenOptions> options)
    {
        _raiz = Path.GetFullPath(options.Value.RutaAlmacenamiento);
    }

    public async Task<VerificacionDocumento> VerificarAsync(
        DocumentoReferencia documento,
        CancellationToken cancellationToken = default)
    {
        if (documento is null)
            return new(false, "La referencia del documento es nula.");

        if (string.IsNullOrWhiteSpace(documento.RutaAlmacenamiento))
            return new(false, "La referencia no incluye una ruta de almacenamiento.");

        if (Path.IsPathRooted(documento.RutaAlmacenamiento))
            return new(false, "La ruta del documento debe ser relativa al volumen compartido.");

        if (string.IsNullOrWhiteSpace(documento.HashSha256))
            return new(false, "La referencia no incluye el SHA-256 del documento.");

        if (documento.TamanoBytes < 0)
            return new(false, "El tamaño del documento es inválido.");

        var ruta = Path.GetFullPath(Path.Combine(_raiz, documento.RutaAlmacenamiento));
        var relativa = Path.GetRelativePath(_raiz, ruta);

        if (Path.IsPathRooted(relativa) ||
            relativa == ".." ||
            relativa.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            relativa.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            return new(false, "La ruta del documento intenta salir del volumen compartido.");
        }

        var archivo = new FileInfo(ruta);
        if (!archivo.Exists)
            return new(false, "El archivo no existe en el volumen compartido.");

        if (archivo.LinkTarget is not null)
            return new(false, "No se aceptan enlaces simbólicos como documentos.");

        if (documento.TamanoBytes > 0 && archivo.Length != documento.TamanoBytes)
            return new(false, $"El tamaño no coincide: esperado {documento.TamanoBytes}, encontrado {archivo.Length}.");

        await using var stream = archivo.OpenRead();
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        var hashHex = Convert.ToHexString(hash).ToLowerInvariant();

        if (!string.Equals(hashHex, documento.HashSha256, StringComparison.OrdinalIgnoreCase))
            return new(false, "El SHA-256 del archivo no coincide con el publicado por el Microservicio B.");

        return new(true, null);
    }
}
