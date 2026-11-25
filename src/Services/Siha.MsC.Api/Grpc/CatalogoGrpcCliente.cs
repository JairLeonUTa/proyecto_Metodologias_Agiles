using Grpc.Core;
using Siha.MsA.Api.Grpc;
using Siha.MsC.Api.Contracts.Dtos;

namespace Siha.MsC.Api.Grpc;

/// <summary>El agente obtiene de MS A (gRPC) la malla vigente de una carrera con unidades y contenidos.</summary>
public interface ICatalogoGrpcCliente
{
    /// <summary>Asignaturas de la malla vigente; vacío si la carrera no existe o no tiene malla.</summary>
    Task<IReadOnlyList<DestinoAnalisisRequest>> ObtenerMallaAsync(string carreraCodigo, CancellationToken cancellationToken = default);
}

public sealed class CatalogoGrpcCliente : ICatalogoGrpcCliente
{
    private readonly CatalogoGrpc.CatalogoGrpcClient _cliente;

    public CatalogoGrpcCliente(CatalogoGrpc.CatalogoGrpcClient cliente) => _cliente = cliente;

    public async Task<IReadOnlyList<DestinoAnalisisRequest>> ObtenerMallaAsync(string carreraCodigo, CancellationToken cancellationToken = default)
    {
        try
        {
            var malla = await _cliente.ObtenerMallaVigenteAsync(
                new SolicitudMalla { CarreraCodigo = carreraCodigo },
                deadline: DateTime.UtcNow.AddSeconds(15), cancellationToken: cancellationToken);
            if (!malla.Encontrada) return Array.Empty<DestinoAnalisisRequest>();

            return malla.Asignaturas
                .Select(a => new DestinoAnalisisRequest(a.Codigo, a.Nombre, a.Creditos, a.Semestre,
                    a.Unidades.Select(u => new UnidadCatalogoRequest(u.Numero, u.Nombre, u.Horas, u.Contenidos)).ToList()))
                .ToList();
        }
        catch (RpcException ex)
        {
            throw new ReglasGrpcException($"No se pudo obtener la malla de {carreraCodigo}: {ex.Status.Detail}", ex.StatusCode, ex);
        }
    }
}
