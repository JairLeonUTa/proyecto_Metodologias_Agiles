using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Siha.Contracts;
using Siha.MsB.Api.Data;
using Siha.MsB.Api.Options;

namespace Siha.MsB.Api.Services;

/// <summary>
/// Recuperación automática tras una caída del bus. Cada <c>Republicacion:IntervaloSegundos</c> busca
/// expedientes en <c>REGISTRADO_PENDIENTE_PUBLICACION</c> (o abandonados en <c>PUBLICACION_EN_CURSO</c>)
/// y los reenvía con <see cref="ExpedienteEnvioService"/>, que vuelve a verificar los documentos en
/// disco y conserva el CorrelationId original. Si un reintento falla, el barrido se detiene hasta el
/// siguiente ciclo (el bus probablemente sigue caído).
/// </summary>
public class RepublicacionAutomaticaWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RepublicacionOptions _opciones;
    private readonly ILogger<RepublicacionAutomaticaWorker> _logger;

    public RepublicacionAutomaticaWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<RepublicacionOptions> opciones,
        ILogger<RepublicacionAutomaticaWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _opciones = opciones.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opciones.Habilitada)
        {
            _logger.LogInformation("Reintento automático de publicaciones deshabilitado.");
            return;
        }

        var intervalo = TimeSpan.FromSeconds(Math.Max(1, _opciones.IntervaloSegundos));
        _logger.LogInformation("Reintento automático de publicaciones activo cada {Intervalo}.", intervalo);

        using var temporizador = new PeriodicTimer(intervalo);

        while (await EsperarSiguienteCicloAsync(temporizador, stoppingToken))
        {
            try
            {
                await EjecutarBarridoAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "El barrido de republicación falló; se reintentará en el siguiente ciclo.");
            }
        }
    }

    /// <summary>Ejecuta un barrido. Público para poder invocarlo desde las pruebas.</summary>
    public async Task<int> EjecutarBarridoAsync(CancellationToken cancellationToken)
    {
        List<Guid> pendientes;
        var limiteAbandono = DateTimeOffset.UtcNow.AddSeconds(-_opciones.EnCursoAbandonadoSegundos);

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SihaDbContextB>();
            var candidatos = await db.Expedientes
                .AsNoTracking()
                .Where(e => !e.EventoPublicado &&
                            (e.Estado == ExpedienteEstados.FalloPublicacion ||
                             e.Estado == ExpedienteEstados.PublicacionEnCurso))
                .Select(e => new { e.Id, e.Estado, e.FechaUltimoIntentoUtc, e.FechaCreacionUtc })
                .ToListAsync(cancellationToken);

            // El filtro por fecha se hace en memoria: DateTimeOffset no se traduce igual en todos los proveedores.
            pendientes = candidatos
                .Where(e => e.Estado == ExpedienteEstados.FalloPublicacion ||
                            (e.FechaUltimoIntentoUtc ?? e.FechaCreacionUtc) < limiteAbandono)
                .OrderBy(e => e.FechaUltimoIntentoUtc ?? e.FechaCreacionUtc)
                .Take(Math.Max(1, _opciones.LoteMaximo))
                .Select(e => e.Id)
                .ToList();
        }

        var publicados = 0;

        foreach (var id in pendientes)
        {
            using var scope = _scopeFactory.CreateScope();
            var envio = scope.ServiceProvider.GetRequiredService<ExpedienteEnvioService>();
            var resultado = await envio.EnviarAsync(id, ModoEnvio.Reintento, cancellationToken);

            _logger.LogInformation(
                "Reintento automático. ExpedienteId={ExpedienteId} CorrelationId={CorrelationId} Resultado={Resultado}",
                id, resultado.Expediente?.CorrelationId, resultado.Tipo);

            if (resultado.Tipo == TipoResultadoEnvio.Publicado)
            {
                publicados++;
            }
            else if (resultado.Tipo == TipoResultadoEnvio.FalloPublicacion)
            {
                break;
            }
        }

        return publicados;
    }

    private static async Task<bool> EsperarSiguienteCicloAsync(PeriodicTimer temporizador, CancellationToken token)
    {
        try
        {
            return await temporizador.WaitForNextTickAsync(token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
