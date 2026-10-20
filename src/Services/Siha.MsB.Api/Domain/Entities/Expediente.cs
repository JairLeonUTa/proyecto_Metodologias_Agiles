using Siha.Contracts;

namespace Siha.MsB.Api.Domain.Entities;

/// <summary>
/// Expediente de homologación persistido en SQL Server DB B.
/// Conserva el <see cref="CorrelationId"/> para la trazabilidad extremo a extremo (US-06, CA3).
/// </summary>
public class Expediente
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Código legible del expediente (prefijo + secuencia).</summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>INTERNO o EXTERNO.</summary>
    public string TipoTramite { get; set; } = string.Empty;

    /// <summary>CAU-P-011 o CAU-P-122.</summary>
    public string RutaNormativa { get; set; } = string.Empty;

    public string CarreraOrigenCodigo { get; set; } = string.Empty;

    public string CarreraDestinoCodigo { get; set; } = string.Empty;

    public string InstitucionOrigen { get; set; } = string.Empty;

    /// <summary>Estado del ciclo de vida (ver <see cref="ExpedienteEstados"/>).</summary>
    public string Estado { get; set; } = ExpedienteEstados.Registrado;

    /// <summary>Identificador de correlación usado como cabecera del mensaje en RabbitMQ.</summary>
    public Guid CorrelationId { get; set; } = Guid.NewGuid();

    public bool EventoPublicado { get; set; }

    public int IntentosPublicacion { get; set; }

    public string? UltimoErrorPublicacion { get; set; }

    public DateTimeOffset FechaCreacionUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? FechaPublicacionUtc { get; set; }

    /// <summary>Momento del último intento de publicación (permite detectar envíos abandonados).</summary>
    public DateTimeOffset? FechaUltimoIntentoUtc { get; set; }

    public string? Observaciones { get; set; }

    /// <summary>
    /// Sello de concurrencia optimista. Se renueva en cada transición de estado relevante para que dos
    /// envíos simultáneos del mismo expediente no publiquen dos veces (el segundo recibe 409).
    /// </summary>
    public Guid SelloConcurrencia { get; set; } = Guid.NewGuid();

    public ICollection<ExpedienteDocumento> Documentos { get; set; } = new List<ExpedienteDocumento>();

    public ICollection<ExpedienteAsignatura> Asignaturas { get; set; } = new List<ExpedienteAsignatura>();
}
