namespace Siha.MsC.Api.Domain.Entities;

/// <summary>Unidad temática extraída de un plan analítico (tabla EXTRACTION_UNIT, DB C).</summary>
public class ExtraccionUnidad
{
    public int Id { get; set; }

    public Guid ExtraccionId { get; set; }
    public Extraccion? Extraccion { get; set; }

    public int Numero { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public int Horas { get; set; }

    public string Contenidos { get; set; } = string.Empty;
}
