namespace Siha.MsA.Api.Domain.Entities;

/// <summary>Carrera ofertada por la FISEI (Software, TI, Industrial, Robótica, Telecomunicaciones).</summary>
public class Carrera
{
    public int Id { get; set; }

    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public string Facultad { get; set; } = "FISEI";

    public bool Activa { get; set; } = true;

    public ICollection<MallaCurricular> Mallas { get; set; } = new List<MallaCurricular>();
}
