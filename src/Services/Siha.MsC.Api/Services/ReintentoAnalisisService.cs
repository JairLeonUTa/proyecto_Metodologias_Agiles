using Microsoft.EntityFrameworkCore;
using Siha.MsC.Api.Data;
using Siha.MsC.Api.Domain.Entities;

namespace Siha.MsC.Api.Services;

/// <summary>
/// Reintenta solas las solicitudes que quedaron en PREPARANDO porque el agente (IA o reglas) no estaba
/// listo, por ejemplo la primera vez que Ollama descarga el modelo. Revisa cada <see cref="Intervalo"/>
/// las que ya cumplieron su espera y las vuelve a analizar; nadie tiene que pulsar "Reintentar".
/// </summary>
public sealed class ReintentoAnalisisService : BackgroundService
{
    public static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(20);
    private const int PorRonda = 5;

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ReintentoAnalisisService> _logger;

    public ReintentoAnalisisService(IServiceScopeFactory scopes, ILogger<ReintentoAnalisisService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await ProgramarActualizacionAsync(_scopes, _logger, stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "No se pudo revisar si hay análisis hechos con reglas anteriores.");
        }

        using var reloj = new PeriodicTimer(Intervalo);
        while (await EsperarAsync(reloj, stoppingToken))
        {
            try
            {
                await ReintentarPendientesAsync(_scopes, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Un fallo de una ronda (por ejemplo, la base todavía no está lista) no detiene el servicio.
                _logger.LogWarning(ex, "No se pudo revisar las solicitudes que esperan al agente; se intentará en la próxima ronda.");
            }
        }
    }

    private static async Task<bool> EsperarAsync(PeriodicTimer reloj, CancellationToken token)
    {
        try
        {
            return await reloj.WaitForNextTickAsync(token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// Al arrancar: las solicitudes analizadas con reglas anteriores del agente y sin ninguna decisión de los
    /// docentes pasan a PREPARANDO para que se vuelvan a analizar solas con las reglas vigentes. Las que ya
    /// tienen decisiones no se tocan: el operador o el administrador pueden volver a analizarlas a mano.
    /// </summary>
    public static async Task<int> ProgramarActualizacionAsync(IServiceScopeFactory scopes, ILogger logger, CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SihaDbContextC>();
        var conDecision = db.DictamenesDocentes.Select(d => d.ExpedienteId);
        var analizados = await db.AnalisisExpedientes
            .Where(a => a.Estado == EstadosAnalisis.Analizado && a.ResultadoJson != null && !conDecision.Contains(a.ExpedienteId))
            .ToListAsync(cancellationToken);

        var desactualizados = analizados.Where(a => AnalisisAutomaticoService.EstaDesactualizado(a.ResultadoJson)).ToList();
        foreach (var a in desactualizados)
        {
            a.Estado = EstadosAnalisis.Preparando;
            a.Mensaje = AnalisisAutomaticoService.MensajeActualizando;
            a.Intentos = 0;
            a.ProximoIntentoUtc = DateTimeOffset.UtcNow;
        }

        if (desactualizados.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("{Cantidad} solicitud(es) analizadas con reglas anteriores se volverán a analizar.", desactualizados.Count);
        }

        return desactualizados.Count;
    }

    /// <summary>Una ronda: analiza de nuevo las solicitudes en PREPARANDO cuya espera ya terminó.</summary>
    public static async Task<int> ReintentarPendientesAsync(IServiceScopeFactory scopes, CancellationToken cancellationToken)
    {
        List<Guid> pendientes;
        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SihaDbContextC>();
            var ahora = DateTimeOffset.UtcNow;
            pendientes = await db.AnalisisExpedientes.AsNoTracking()
                .Where(a => a.Estado == EstadosAnalisis.Preparando && (a.ProximoIntentoUtc == null || a.ProximoIntentoUtc <= ahora))
                .OrderBy(a => a.ProximoIntentoUtc)
                .Select(a => a.ExpedienteId)
                .Take(PorRonda)
                .ToListAsync(cancellationToken);
        }

        foreach (var expedienteId in pendientes)
        {
            // Un scope por solicitud: cada análisis usa su propio DbContext.
            using var scope = scopes.CreateScope();
            var agente = scope.ServiceProvider.GetRequiredService<IAnalisisAutomaticoService>();
            await agente.AnalizarYGuardarAsync(expedienteId, cancellationToken);
        }

        return pendientes.Count;
    }
}
