using MassTransit;
using Siha.Contracts.Events;

namespace Siha.MsB.Api.Events;

/// <summary>Publicador del evento de expediente hacia el bus (US-06, tarea S1-J07/J08).</summary>
public interface IExpedienteEventPublisher
{
    /// <summary>
    /// Publica <see cref="SolicitudCreadaEvent"/> con reintentos. Devuelve (true, null) si se publicó
    /// y (false, error) si se agotaron los intentos, preservando siempre el identificador de correlación.
    /// </summary>
    Task<(bool Ok, string? Error)> PublicarAsync(SolicitudCreadaEvent evento, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementación resiliente sobre MassTransit/RabbitMQ. Aplica una política de reintento con
/// espera creciente y registra cada fallo sin perder la trazabilidad del CorrelationId (CA3).
/// </summary>
public class ExpedienteEventPublisher : IExpedienteEventPublisher
{
    public const int MaxIntentos = 3;

    /// <summary>
    /// Tiempo máximo que se espera por cada intento de publicación. Sin este límite, cuando el bus
    /// está caído MassTransit espera indefinidamente a restablecer la conexión y la petición HTTP
    /// nunca termina, por lo que el expediente jamás llega al estado recuperable (CA3, tarea S1-J08).
    /// </summary>
    public static readonly TimeSpan TiempoMaximoPorIntento = TimeSpan.FromSeconds(5);

    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<ExpedienteEventPublisher> _logger;

    public ExpedienteEventPublisher(IPublishEndpoint publishEndpoint, ILogger<ExpedienteEventPublisher> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task<(bool Ok, string? Error)> PublicarAsync(
        SolicitudCreadaEvent evento,
        CancellationToken cancellationToken = default)
    {
        Exception? ultimoError = null;

        for (var intento = 1; intento <= MaxIntentos; intento++)
        {
            try
            {
                using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                limite.CancelAfter(TiempoMaximoPorIntento);

                var tentativa = _publishEndpoint.Publish(
                    evento,
                    context =>
                    {
                        context.CorrelationId = evento.CorrelationId;

                        // MessageId determinista (= ExpedienteId): si un reintento llega a publicar dos
                        // veces el mismo expediente, el consumidor puede descartar el duplicado.
                        context.MessageId = evento.ExpedienteId;
                    },
                    limite.Token);

                // Se observa el resultado para que un fallo posterior no quede como excepción no controlada.
                _ = tentativa.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);

                // Aunque el transporte ignore la cancelación, la petición HTTP queda acotada por el vencimiento.
                if (await Task.WhenAny(tentativa, Task.Delay(TiempoMaximoPorIntento, CancellationToken.None)) != tentativa)
                {
                    throw new TimeoutException(
                        $"El bus no respondió en {TiempoMaximoPorIntento.TotalSeconds:0} s.");
                }

                await tentativa;

                _logger.LogInformation(
                    "SolicitudCreadaEvent publicado. ExpedienteId={ExpedienteId} CorrelationId={CorrelationId} Intento={Intento}",
                    evento.ExpedienteId, evento.CorrelationId, intento);

                return (true, null);
            }
            catch (Exception ex)
            {
                ultimoError = ex;
                _logger.LogWarning(
                    ex,
                    "Fallo al publicar SolicitudCreadaEvent. ExpedienteId={ExpedienteId} CorrelationId={CorrelationId} Intento={Intento}/{MaxIntentos}",
                    evento.ExpedienteId, evento.CorrelationId, intento, MaxIntentos);

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (intento < MaxIntentos)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(300 * intento), cancellationToken);
                }
            }
        }

        return (false, ultimoError?.Message);
    }
}
