using Grpc.Core;
using Siha.MsA.Api.Grpc;

namespace Siha.MsB.Api.Services;

/// <summary>Estado de un docente revisor según MS A.</summary>
public sealed record DocenteRevisorVerificado(string Correo, bool Existe, bool EsDocente, bool Activo, string NombreCompleto)
{
    public bool Valido => Existe && EsDocente && Activo;
}

public sealed class VerificacionDocentesNoDisponibleException(string mensaje, Exception interna) : Exception(mensaje, interna);

/// <summary>Comprueba en MS A (gRPC) que los revisores del certificado son usuarios DOCENTE activos.</summary>
public interface IVerificadorDocentes
{
    Task<IReadOnlyList<DocenteRevisorVerificado>> VerificarAsync(IEnumerable<string> correos, CancellationToken cancellationToken = default);
}

public sealed class VerificadorDocentesGrpc : IVerificadorDocentes
{
    private readonly CatalogoGrpc.CatalogoGrpcClient _cliente;

    public VerificadorDocentesGrpc(CatalogoGrpc.CatalogoGrpcClient cliente) => _cliente = cliente;

    public async Task<IReadOnlyList<DocenteRevisorVerificado>> VerificarAsync(
        IEnumerable<string> correos, CancellationToken cancellationToken = default)
    {
        var solicitud = new SolicitudDocentes();
        solicitud.Correos.AddRange(correos);
        try
        {
            var respuesta = await _cliente.VerificarDocentesAsync(
                solicitud, deadline: DateTime.UtcNow.AddSeconds(10), cancellationToken: cancellationToken);
            return respuesta.Docentes
                .Select(d => new DocenteRevisorVerificado(d.Correo, d.Existe, d.EsDocente, d.Activo, d.NombreCompleto))
                .ToList();
        }
        catch (RpcException ex)
        {
            throw new VerificacionDocentesNoDisponibleException(
                "No se pudo verificar los docentes revisores en el servicio de usuarios (MS A). Intente nuevamente.", ex);
        }
    }
}
