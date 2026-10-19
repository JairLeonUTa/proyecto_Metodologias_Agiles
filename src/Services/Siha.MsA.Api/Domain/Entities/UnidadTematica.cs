namespace Siha.MsA.Api.Domain.Entities;

/// <summary>Unidad temática de una asignatura, con su carga horaria y contenidos.</summary>
public class UnidadTematica
{
    public int Id { get; set; }

    public int AsignaturaId { get; set; }
    public Asignatura? Asignatura { get; set; }

    public int Numero { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public int Horas { get; set; }

    public string Contenidos { get; set; } = string.Empty;
}
