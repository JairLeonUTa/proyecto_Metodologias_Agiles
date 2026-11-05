using Grpc.Core;
using Siha.MsA.Api.Grpc;

namespace Siha.MsB.Api.Services;

/// <summary>Estado de un docente revisor según MS A.</summary>
public sealed record DocenteRevisorVerificado(string Correo, bool Existe, bool EsDocente, bool Activo, string NombreCompleto)
{
    public bool Valido => Existe && EsDocente && Activo;
}

/// <summary>Carrera del catálogo de MS A encontrada por su nombre completo.</summary>
public sealed record CarreraCatalogo(string Codigo, string Nombre);

public sealed class VerificacionDocentesNoDisponibleException(string mensaje, Exception interna) : Exception(mensaje, interna);

/// <summary>
/// Consultas a MS A (gRPC) que necesita el registro del certificado: que los revisores sean usuarios
/// DOCENTE activos y que las carreras escritas con su nombre completo existan en el catálogo.
/// </summary>
public interface IVerificadorDocentes
{
    Task<IReadOnlyList<DocenteRevisorVerificado>> VerificarAsync(IEnumerable<string> correos, CancellationToken cancellationToken = default);

    /// <summary>Carrera por nombre ("Software" = "Ingeniería de Software"); null si no existe.</summary>
    Task<CarreraCatalogo?> ResolverCarreraAsync(string nombre, CancellationToken cancellationToken = default);
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

    public async Task<CarreraCatalogo?> ResolverCarreraAsync(string nombre, CancellationToken cancellationToken = default)
    {
        try
        {
            var r = await _cliente.ResolverCarreraAsync(
                new SolicitudCarrera { Nombre = nombre }, deadline: DateTime.UtcNow.AddSeconds(10), cancellationToken: cancellationToken);
            return r.Encontrada ? new CarreraCatalogo(r.Codigo, r.Nombre) : null;
        }
        catch (RpcException ex)
        {
            throw new VerificacionDocentesNoDisponibleException(
                "No se pudo consultar el catálogo de carreras (MS A). Intente nuevamente.", ex);
        }
    }
}
