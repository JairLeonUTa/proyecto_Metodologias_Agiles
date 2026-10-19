using System.Text.Json;
using MassTransit;
using Siha.Contracts.Events;

namespace Siha.BusVerifier;

/// <summary>
/// Suscriptor de prueba del contrato <see cref="SolicitudCreadaEvent"/> (Sprint 1, tarea S1-P04).
/// Imprime en pantalla el mensaje completo recibido desde RabbitMQ: cuerpo JSON con todos los
/// campos del contrato y los metadatos de transporte de MassTransit (CorrelationId, MessageId,
/// cola de entrada y cabeceras), que son la evidencia del flujo B → RabbitMQ → C.
/// </summary>
public sealed class VerificadorSolicitudCreadaConsumer : IConsumer<SolicitudCreadaEvent>
{
    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        WriteIndented = true
    };

    public Task Consume(ConsumeContext<SolicitudCreadaEvent> context)
    {
        var evento = context.Message;
        var recibidoUtc = DateTimeOffset.UtcNow;

        Console.WriteLine();
        Console.WriteLine(new string('=', 86));
        Console.WriteLine($"  SolicitudCreadaEvent RECIBIDO · {recibidoUtc:yyyy-MM-dd HH:mm:ss.fff} UTC");
        Console.WriteLine(new string('=', 86));

        Console.WriteLine("--- Resumen del contrato ------------------------------------------------");
        Console.WriteLine($"  CodigoExpediente   : {evento.CodigoExpediente}");
        Console.WriteLine($"  ExpedienteId       : {evento.ExpedienteId}");
        Console.WriteLine($"  CorrelationId      : {evento.CorrelationId}");
        Console.WriteLine($"  TipoTramite        : {evento.TipoTramite}");
        Console.WriteLine($"  RutaNormativa      : {evento.RutaNormativa}");
        Console.WriteLine($"  CarreraOrigen      : {evento.CarreraOrigenCodigo}");
        Console.WriteLine($"  CarreraDestino     : {evento.CarreraDestinoCodigo}");
        Console.WriteLine($"  Estado             : {evento.Estado}");
        Console.WriteLine($"  Version            : {evento.Version}");
        Console.WriteLine($"  FechaCreacionUtc   : {evento.FechaCreacionUtc:O}");
        Console.WriteLine($"  DocumentoIds       : {evento.DocumentoIds.Count} referencia(s) (sin binarios adjuntos)");
        foreach (var documentoId in evento.DocumentoIds)
        {
            Console.WriteLine($"      · {documentoId}");
        }

        Console.WriteLine($"  AsignaturasOrigen  : {evento.AsignaturasOrigen.Count} código(s)");
        foreach (var asignatura in evento.AsignaturasOrigen)
        {
            Console.WriteLine($"      · {asignatura}");
        }

        Console.WriteLine();
        Console.WriteLine("--- Cuerpo JSON del mensaje ---------------------------------------------");
        Console.WriteLine(JsonSerializer.Serialize(evento, OpcionesJson));

        Console.WriteLine();
        Console.WriteLine("--- Metadatos de transporte (MassTransit / RabbitMQ) --------------------");
        Console.WriteLine($"  MessageId          : {context.MessageId}");
        Console.WriteLine($"  CorrelationId      : {context.CorrelationId}");
        Console.WriteLine($"  ConversationId     : {context.ConversationId}");
        Console.WriteLine($"  RequestId          : {context.RequestId}");
        Console.WriteLine($"  Cola de entrada    : {context.ReceiveContext.InputAddress}");
        Console.WriteLine($"  Reintento          : {context.GetRetryAttempt()}");

        if (context.Headers is not null)
        {
            foreach (var cabecera in context.Headers.GetAll())
            {
                Console.WriteLine($"  header             : {cabecera.Key} = {cabecera.Value}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"  ✔ Contrato verificado: el evento no adjunta archivos binarios, solo {evento.DocumentoIds.Count} referencia(s).");
        Console.WriteLine(new string('-', 86));

        NotificadorRecepcion.Notificar(evento);
        return Task.CompletedTask;
    }
}
