using Microsoft.Extensions.Options;
using Siha.Certificados;
using Siha.MsC.Api.Options;

namespace Siha.MsC.Api.Services;

/// <summary>Lee del volumen compartido un PDF y, si es un certificado SIHA válido, devuelve sus datos.</summary>
public interface ILectorCertificadosAlmacen
{
    Task<CertificadoSiha?> LeerAsync(string rutaRelativa, CancellationToken cancellationToken = default);
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
        var lectura = LectorCertificadoSiha.Leer(bytes);
        return lectura.EsValido ? lectura.Certificado : null;
    }
}
