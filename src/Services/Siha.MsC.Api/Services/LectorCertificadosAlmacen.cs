using Microsoft.Extensions.Options;
using Siha.Certificados;
using Siha.MsC.Api.Options;

namespace Siha.MsC.Api.Services;

/// <summary>Lee del volumen compartido un PDF y, si es un certificado SIHA válido, devuelve sus datos.</summary>
public interface ILectorCertificadosAlmacen
{
    Task<CertificadoSiha?> LeerAsync(string rutaRelativa, CancellationToken cancellationToken = default);

    /// <summary>Lee el sílabo externo de una materia (movilidad externa) desde el volumen compartido.</summary>
    Task<SilaboExterno?> LeerSilaboAsync(string rutaRelativa, string materia, CancellationToken cancellationToken = default);
}

public sealed class LectorCertificadosAlmacen : ILectorCertificadosAlmacen
{
    private readonly string _raiz;

    public LectorCertificadosAlmacen(IOptions<AlmacenOptions> opciones)
    {
        _raiz = Path.GetFullPath(opciones.Value.RutaAlmacenamiento);
    }

    public async Task<CertificadoSiha?> LeerAsync(string rutaRelativa, CancellationToken cancellationToken = default)
    {
        var ruta = Path.GetFullPath(Path.Combine(_raiz, rutaRelativa));
        if (!ruta.StartsWith(_raiz, StringComparison.Ordinal) || !File.Exists(ruta))
        {
            return null;
        }

        var bytes = await File.ReadAllBytesAsync(ruta, cancellationToken);

        // Registro manual (sin PDF): MS B guarda los datos ya validados como JSON.
        if (EsJson(bytes))
        {
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<CertificadoSiha>(bytes);
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }
        }

        var lectura = LectorCertificadoSiha.Leer(bytes);
        return lectura.EsValido ? lectura.Certificado : null;
    }

    /// <summary>True si el archivo es JSON (empieza con "{", con o sin BOM y espacios).</summary>
    public static bool EsJson(byte[] bytes)
    {
        var i = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        while (i < bytes.Length && (bytes[i] == ' ' || bytes[i] == '\n' || bytes[i] == '\r' || bytes[i] == '\t')) i++;
        return i < bytes.Length && bytes[i] == '{';
    }

    public async Task<SilaboExterno?> LeerSilaboAsync(
        string rutaRelativa, string materia, CancellationToken cancellationToken = default)
    {
        var ruta = Path.GetFullPath(Path.Combine(_raiz, rutaRelativa));
        if (!ruta.StartsWith(_raiz, StringComparison.Ordinal) || !File.Exists(ruta))
        {
            return null;
        }

        var bytes = await File.ReadAllBytesAsync(ruta, cancellationToken);
        var lectura = LectorSilaboExterno.Leer(bytes, materia);
        return lectura.EsValido ? lectura.Silabo : null;
    }
}
