using Grpc.Core;
using Siha.Contracts;
using Siha.MsA.Api.Grpc;
using Siha.MsA.Api.Services;

namespace Siha.MsA.Api.GrpcServices;

/// <summary>
/// Servidor gRPC de reglas y umbrales (Sprint 1 - S1-S04).
///
/// Reutiliza <see cref="IReglasService"/> (la misma fuente de verdad que los endpoints REST
/// de /api/reglas) para devolver, en una sola llamada, los umbrales θ, δ, τ vigentes y las
/// reglas configuradas de una ruta normativa (CAU-P-011 o CAU-P-122).
///
/// Errores: se responden como <see cref="RpcException"/> con <c>StatusCode.InvalidArgument</c>
/// cuando la ruta normativa o el tipo de trámite no son válidos.
/// </summary>
public class ReglasGrpcService : ReglasGrpc.ReglasGrpcBase
{
    private readonly IReglasService _reglasService;
    private readonly ILogger<ReglasGrpcService> _logger;

    public ReglasGrpcService(IReglasService reglasService, ILogger<ReglasGrpcService> logger)
    {
        _reglasService = reglasService;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<ConfiguracionRuta> ObtenerConfiguracionRuta(
        SolicitudConfiguracionRuta request,
        ServerCallContext context)
    {
        var rutaNormativa = (request.RutaNormativa ?? string.Empty).Trim();

        if (!RutasNormativas.Todas.Contains(rutaNormativa))
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                $"Ruta normativa inválida. Valores permitidos: {string.Join(", ", RutasNormativas.Todas)}."));
        }

        var tipoTramite = string.IsNullOrWhiteSpace(request.TipoTramite) ? null : request.TipoTramite.Trim();

        if (tipoTramite is not null && !TiposTramite.Todos.Contains(tipoTramite))
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                $"Tipo de trámite inválido. Valores permitidos: {string.Join(", ", TiposTramite.Todos)}."));
        }

        var parametros = await _reglasService.ObtenerParametrosAsync(rutaNormativa);
        var reglas = await _reglasService.ListarReglasAsync(rutaNormativa, tipoTramite);

        _logger.LogInformation(
            "gRPC ObtenerConfiguracionRuta. Ruta={Ruta} TipoTramite={TipoTramite} Reglas={Reglas} ParametrosConfigurados={Configurados}",
            rutaNormativa, tipoTramite ?? "(todas)", reglas.Count, parametros is not null);

        var respuesta = new ConfiguracionRuta
        {
            RutaNormativa = rutaNormativa,
            ParametrosConfigurados = parametros is not null
        };

        if (parametros is not null)
        {
            respuesta.Parametros = new ParametrosEvaluacionMensaje
            {
                Id = parametros.Id,
                RutaNormativa = parametros.RutaNormativa,
                Theta = parametros.Theta,
                Delta = parametros.Delta,
                Tau = parametros.Tau,
                Version = parametros.Version,
                ActualizadoUtc = parametros.ActualizadoUtc.ToString("O"),
                ActualizadoPor = parametros.ActualizadoPor
            };
        }

        respuesta.Reglas.AddRange(reglas.Select(ReglaAMensaje));

        return respuesta;
    }

    private static ReglaMensaje ReglaAMensaje(Contracts.Dtos.ReglaDto regla) => new()
    {
        Id = regla.Id,
        Codigo = regla.Codigo,
        RutaNormativa = regla.RutaNormativa,
        TipoTramite = regla.TipoTramite,
        Descripcion = regla.Descripcion,
        Expresion = regla.Expresion ?? string.Empty,
        Version = regla.Version,
        Fuente = regla.Fuente,
        EsNormativa = regla.EsNormativa,
        VigenteDesde = regla.VigenteDesde.ToString("yyyy-MM-dd"),
        VigenteHasta = regla.VigenteHasta?.ToString("yyyy-MM-dd") ?? string.Empty,
        Activa = regla.Activa
    };
}
