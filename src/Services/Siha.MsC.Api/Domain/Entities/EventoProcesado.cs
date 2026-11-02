namespace Siha.MsC.Api.Domain.Entities;

/// <summary>
/// Registro de idempotencia del Microservicio C (tabla PROCESSED_EVENTS).
/// La clave es el ExpedienteId, que el publicador usa también como MessageId determinista.
/// </summary>
public class EventoProcesado
{
    public Guid ExpedienteId { get; set; }

    public Guid MessageId { get; set; }

    public Guid CorrelationId { get; set; }

    public string TipoEvento { get; set; } = string.Empty;

    public DateTimeOffset RecibidoUtc { get; set; } = DateTimeOffset.UtcNow;
}
