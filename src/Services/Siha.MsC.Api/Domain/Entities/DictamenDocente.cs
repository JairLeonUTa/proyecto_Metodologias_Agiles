namespace Siha.MsC.Api.Domain.Entities;

/// <summary>
/// Respuesta de uno de los dos docentes revisores (tabla DICTAMEN_DOCENTE, DB C): por cada materia de
/// destino indica si aprueba o no la homologación, esté o no de acuerdo con la estimación del agente.
/// </summary>
public class DictamenDocente
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExpedienteId { get; set; }
    public string CorreoDocente { get; set; } = string.Empty;
    public string NombreDocente { get; set; } = string.Empty;

    /// <summary>Lista de <c>DecisionMateria</c> serializada.</summary>
    public string DecisionesJson { get; set; } = "[]";

    public string? Comentario { get; set; }
    public DateTimeOffset FechaUtc { get; set; } = DateTimeOffset.UtcNow;
}
