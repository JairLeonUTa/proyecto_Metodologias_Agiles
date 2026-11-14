namespace Siha.MsC.Api.Domain.Entities;

/// <summary>
/// Resultado de homologación por materia (tabla RESULTADO_HOMOLOGACION, DB C): una fila por cada materia
/// que el estudiante pide homologar, con los porcentajes que calculó el agente y la decisión final de
/// los dos docentes revisores. Se llena cuando el agente termina el análisis y se actualiza cada vez
/// que un docente responde. Sirve para consultar y reportar las homologaciones con SQL, sin leer JSON.
/// </summary>
public class ResultadoHomologacion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ExpedienteId { get; set; }
    public string CodigoExpediente { get; set; } = string.Empty;
    public string NumeroCertificado { get; set; } = string.Empty;
    public string EstudianteNombre { get; set; } = string.Empty;

    /// <summary>INTERNO (cambio de carrera) o EXTERNO (movilidad desde otra universidad).</summary>
    public string TipoTramite { get; set; } = string.Empty;
    public string RutaNormativa { get; set; } = string.Empty;
    public string InstitucionOrigen { get; set; } = string.Empty;
    public string CarreraOrigen { get; set; } = string.Empty;
    public string CarreraDestino { get; set; } = string.Empty;

    /// <summary>Número de la solicitud dentro del certificado (1, 2, 3...).</summary>
    public int NumeroSolicitud { get; set; }

    /// <summary>Materia(s) aprobada(s) que se presentan, unidas con " + " cuando son varias.</summary>
    public string MateriasOrigen { get; set; } = string.Empty;
    public string MateriaDestino { get; set; } = string.Empty;
    public string? CodigoDestino { get; set; }

    /// <summary>Créditos de lo aprobado (suma si son varias materias). Nulo si no se conocen.</summary>
    public int? CreditosOrigen { get; set; }
    public int? CreditosDestino { get; set; }

    /// <summary>Créditos aprobados frente a los de destino (máximo 100). Dato informativo.</summary>
    public decimal? PorcentajeCreditos { get; set; }

    public int? HorasOrigen { get; set; }
    public int HorasDestino { get; set; }

    public decimal PorcentajeContenidos { get; set; }
    public decimal PorcentajeProfundidad { get; set; }
    public decimal PorcentajeCargaHoraria { get; set; }

    /// <summary>Mínimo exigido (θ) con el que se evaluó.</summary>
    public decimal MinimoExigido { get; set; }

    /// <summary>True si no hubo sílabo para comparar: no hay porcentajes y deciden los docentes.</summary>
    public bool SinSilabo { get; set; }

    /// <summary>CUMPLE o CRITERIO_DOCENTE.</summary>
    public string EstimacionAgente { get; set; } = string.Empty;

    /// <summary>Cuántos de los dos docentes aprobaron / rechazaron la homologación.</summary>
    public int Aprobaciones { get; set; }
    public int Rechazos { get; set; }

    /// <summary>PENDIENTE, HOMOLOGADA, NO_HOMOLOGADA o SIN_ACUERDO.</summary>
    public string ResultadoFinal { get; set; } = "PENDIENTE";

    public DateTimeOffset AnalizadoUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ActualizadoUtc { get; set; } = DateTimeOffset.UtcNow;
}
