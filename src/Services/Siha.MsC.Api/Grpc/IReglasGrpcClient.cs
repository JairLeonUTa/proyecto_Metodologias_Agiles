using Grpc.Core;
using Siha.MsA.Api.Grpc;
using Siha.MsC.Api.Options;

namespace Siha.MsC.Api.Grpc;

/// <summary>Cliente tipado para consultar reglas y umbrales en el Microservicio A vía gRPC.</summary>
public interface IReglasGrpcClient
{
    /// <summary>
    /// Obtiene la configuración de una ruta normativa (umbrales θ, δ, τ y reglas asociadas).
    /// </summary>
    /// <param name="rutaNormativa">Código de la ruta (CAU-P-011, CAU-P-122).</param>
    /// <param name="tipoTramite">INTERNO o EXTERNO (opcional).</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Configuración completa o null si la ruta no tiene parámetros configurados.</returns>
    /// <exception cref="ReglasGrpcException">Error de comunicación, timeout o respuesta inválida.</exception>
    Task<ConfiguracionRuta?> ObtenerConfiguracionAsync(
        string rutaNormativa,
        string? tipoTramite = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Excepción lanzada cuando la llamada gRPC a ReglasGrpc falla de forma controlada.</summary>
public sealed class ReglasGrpcException : Exception
{
    public ReglasGrpcException(string message, Exception? inner = null) : base(message, inner) { }

    public ReglasGrpcException(string message, StatusCode statusCode, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }

    public StatusCode? StatusCode { get; }
}