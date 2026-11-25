namespace Siha.MsC.Api.Domain.Entities;

/// <summary>
/// Decisión final del administrador (coordinador o director de carrera) sobre una materia en la que los
/// dos docentes revisores no se pusieron de acuerdo (uno aprobó y el otro no). Tabla DECISION_FINAL (DB C).
/// Solo cuenta mientras la materia siga SIN_ACUERDO: si un docente cambia su voto, decide la votación.
/// </summary>
public class DecisionFinalAdministrador
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExpedienteId { get; set; }

    /// <summary>Materia de destino (la misma clave que usan las decisiones de los docentes).</summary>
    public string Materia { get; set; } = string.Empty;

    public bool Aprobada { get; set; }

    /// <summary>Motivo de la decisión (obligatorio).</summary>
    public string Comentario { get; set; } = string.Empty;

    public string CorreoAdministrador { get; set; } = string.Empty;
    public string NombreAdministrador { get; set; } = string.Empty;
    public DateTimeOffset FechaUtc { get; set; } = DateTimeOffset.UtcNow;
}
