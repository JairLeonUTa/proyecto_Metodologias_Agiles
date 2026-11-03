using MassTransit;
using Siha.Contracts.Events;
using Siha.MsC.Api.Services;

namespace Siha.MsC.Api.Consumers;

/// <summary>
/// Consumidor idempotente del aviso de solicitud creada (S2-J01).
/// MessageId = ExpedienteId (lo fija el publicador del Microservicio B).
/// </summary>
public class SolicitudCreadaConsumer : IConsumer<SolicitudCreadaEvent>
{
    private readonly ProcesarSolicitudCreadaService _servicio;
    private readonly ILogger<SolicitudCreadaConsumer> _logger;

    public SolicitudCreadaConsumer(
        ProcesarSolicitudCreadaService servicio,
        ILogger<SolicitudCreadaConsumer> logger)
    {
        _servicio = servicio;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<SolicitudCreadaEvent> context)
    {
        var messageId = context.MessageId ?? context.Message.ExpedienteId;
        var resultado = await _servicio.ProcesarAsync(
            context.Message, messageId, context.CancellationToken);

        if (resultado == ResultadoProcesamiento.Duplicado)
        {
            _logger.LogDebug(
                "Mensaje ya procesado; se confirma sin repetir trabajo. ExpedienteId={ExpedienteId}",
                context.Message.ExpedienteId);
        }
    }
}
