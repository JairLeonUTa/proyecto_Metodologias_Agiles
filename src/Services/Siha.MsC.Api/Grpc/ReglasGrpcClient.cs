using Grpc.Core;
using Microsoft.Extensions.Options;
using Siha.MsA.Api.Grpc;
using Siha.MsC.Api.Options;

namespace Siha.MsC.Api.Grpc;

/// <summary>
/// Implementación del cliente gRPC hacia <c>siha.reglas.v1.ReglasGrpc</c> (Microservicio A).
///
/// Maneja:
/// - Reintentos exponenciales ante fallos transitorios (red, 5xx, deadline exceeded).
/// - Timeouts configurables.
/// - Mapeo de errores gRPC a <see cref="ReglasGrpcException"/> para que el consumidor
///   decida poner la evaluación en <c>PENDIENTE_REGLAS</c> sin inventar valores.
/// </summary>
public sealed class ReglasGrpcClient : IReglasGrpcClient
{
    private readonly ReglasGrpc.ReglasGrpcClient _grpcClient;
    private readonly IOptionsMonitor<ReglasGrpcOptions> _options;
    private readonly ILogger<ReglasGrpcClient> _logger;

    public ReglasGrpcClient(
        ReglasGrpc.ReglasGrpcClient grpcClient,
        IOptionsMonitor<ReglasGrpcOptions> options,
        ILogger<ReglasGrpcClient> logger)
    {
        _grpcClient = grpcClient;
        _options = options;
        _logger = logger;
    }

    public async Task<ConfiguracionRuta?> ObtenerConfiguracionAsync(
        string rutaNormativa,
        string? tipoTramite = null,
        CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        var request = new SolicitudConfiguracionRuta
        {
            RutaNormativa = rutaNormativa,
            TipoTramite = tipoTramite ?? string.Empty
        };

        var attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                using var call = _grpcClient.ObtenerConfiguracionRutaAsync(
                    request,
                    deadline: DateTime.UtcNow.AddSeconds(opts.TimeoutSegundos),
                    cancellationToken: cancellationToken);

                var response = await call.ResponseAsync;

                _logger.LogInformation(
                    "gRPC ObtenerConfiguracionRuta OK. Ruta={Ruta} TipoTramite={TipoTramite} ParametrosConfigurados={Configurados} Reglas={Reglas} Intento={Intento}",
                    response.RutaNormativa,
                    string.IsNullOrWhiteSpace(response.RutaNormativa) ? "(vacío)" : response.RutaNormativa,
                    response.ParametrosConfigurados,
                    response.Reglas.Count,
                    attempt);

                return response;
            }
            catch (RpcException ex) when (EsTransitorio(ex.StatusCode) && attempt <= opts.MaxRetries)
            {
                var delay = TimeSpan.FromSeconds(opts.RetryBaseDelaySegundos * Math.Pow(2, attempt - 1));
                _logger.LogWarning(
                    ex,
                    "gRPC transitorio (intento {Intento}/{Max}): {StatusCode} - {Detail}. Reintento en {Delay}s.",
                    attempt, opts.MaxRetries, ex.StatusCode, ex.Status.Detail, delay.TotalSeconds);

                await Task.Delay(delay, cancellationToken);
            }
            catch (RpcException ex)
            {
                _logger.LogError(
                    ex,
                    "gRPC error no recuperable: {StatusCode} - {Detail}. Ruta={Ruta} TipoTramite={TipoTramite}",
                    ex.StatusCode, ex.Status.Detail, rutaNormativa, tipoTramite ?? "(todas)");

                throw new ReglasGrpcException(
                    $"Error gRPC consultando reglas: {ex.Status.Detail ?? ex.StatusCode.ToString()}",
                    ex.StatusCode,
                    ex);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado llamando a ReglasGrpc.");
                throw new ReglasGrpcException("Error de comunicación con el servicio de reglas.", inner: ex);
            }
        }
    }

    private static bool EsTransitorio(StatusCode code) => code switch
    {
        StatusCode.Unavailable => true,      // servidor caído / red
        StatusCode.DeadlineExceeded => true, // timeout
        StatusCode.Internal => true,         // 5xx genérico
        StatusCode.ResourceExhausted => true,// 429 / rate limit
        _ => false
    };
}