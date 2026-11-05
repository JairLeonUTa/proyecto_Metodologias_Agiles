namespace Siha.MsC.Api.Domain.Entities;

/// <summary>
/// Datos extraídos de un documento del expediente (tabla EXTRACTION, DB C).
/// El extractor los crea en <c>PENDIENTE_CONFIRMACION</c> cuando la confianza es menor a 0.80
/// o faltan datos obligatorios; el operador los corrige y confirma en S2-J02.
/// </summary>
public class Extraccion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Expediente de DB B al que pertenece la extracción.</summary>
    public Guid ExpedienteId { get; set; }

    /// <summary>Documento PDF del que se extrajeron los datos (referencia de DB B).</summary>
    public Guid? DocumentoId { get; set; }

    /// <summary>Código de la asignatura de origen según el plan analítico.</summary>
    public string CodigoAsignaturaOrigen { get; set; } = string.Empty;

    public string NombreAsignaturaOrigen { get; set; } = string.Empty;

    /// <summary>Nulo cuando el extractor no pudo determinarlos; el operador debe completarlos.</summary>
    public int? Creditos { get; set; }

    public int? Horas { get; set; }

    /// <summary>Confianza global de la extracción en el rango [0, 1].</summary>
    public decimal Confianza { get; set; }

    /// <summary>Ver <see cref="EstadosExtraccion"/>.</summary>
    public string Estado { get; set; } = EstadosExtraccion.PendienteConfirmacion;

    public DateTimeOffset CreadoUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ConfirmadoUtc { get; set; }

    /// <summary>Usuario que confirmó o corrigió los datos.</summary>
    public string? ConfirmadoPor { get; set; }

    public string? Observacion { get; set; }

    public ICollection<ExtraccionUnidad> Unidades { get; set; } = new List<ExtraccionUnidad>();
}
