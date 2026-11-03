namespace Siha.MsC.Api.Domain.Entities;

/// <summary>
/// Precedente de homologación registrado (tabla PRECEDENTE, DB C).
/// Se consulta por coincidencia exacta de asignaturas origen + destino + institución (US-11, S2-J08).
/// </summary>
public class Precedente
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Código de la resolución (ej. RES-FISEI-2025-031).</summary>
    public string Resolucion { get; set; } = string.Empty;

    /// <summary>
    /// JSON con las asignaturas de origen comparadas.
    /// Ej: [{"codigo":"SW101","nombre":"Fundamentos de Programación"},{"codigo":"SW102","nombre":"Programación II"}]
    /// </summary>
    public string AsignaturasOrigenJson { get; set; } = "[]";

    /// <summary>Código de la asignatura de destino homologada.</summary>
    public string AsignaturaDestino { get; set; } = string.Empty;

    /// <summary>Institución de origen del estudiante.</summary>
    public string InstitucionOrigen { get; set; } = string.Empty;

    /// <summary>Fecha de la resolución.</summary>
    public DateOnly Fecha { get; set; }

    public DateTimeOffset CreadoUtc { get; set; } = DateTimeOffset.UtcNow;
}