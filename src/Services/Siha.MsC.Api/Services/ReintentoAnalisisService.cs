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
