using MassTransit;
using Siha.Contracts.Events;

namespace Siha.MsB.Api.Consumers;

/// <summary>
/// Suscriptor de verificación del contrato (equivalente al suscriptor de prueba del Sprint 1).
/// Permite comprobar de extremo a extremo que el mensaje publicado cumple el contrato acordado
/// sin activar aún el análisis del Microservicio C. Se usa como evidencia de la tarea S1-J09.
/// </summary>
public class VerificacionSolicitudConsumer : IConsumer<SolicitudCreadaEvent>
{
    private readonly ILogger<VerificacionSolicitudConsumer> _logger;

    public VerificacionSolicitudConsumer(ILogger<VerificacionSolicitudConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<SolicitudCreadaEvent> context)
    {
        var evento = context.Message;

        _logger.LogInformation(
            "[Verificación] SolicitudCreadaEvent recibido. Codigo={Codigo} ExpedienteId={ExpedienteId} " +
            "CorrelationId={CorrelationId} Ruta={Ruta} Tipo={Tipo} Documentos={Documentos} Asignaturas={Asignaturas} Estado={Estado}",
            evento.CodigoExpediente,
            evento.ExpedienteId,
            evento.CorrelationId,
            evento.RutaNormativa,
            evento.TipoTramite,
            evento.DocumentoIds.Count,
            evento.AsignaturasOrigen.Count,
            evento.Estado);

        return Task.CompletedTask;
    }
}
