namespace Siha.MsA.Api.Domain.Entities;

/// <summary>Versión de malla curricular de una carrera, asociada a su fuente normativa y vigencia.</summary>
public class MallaCurricular
{
    public int Id { get; set; }

    public int CarreraId { get; set; }
    public Carrera? Carrera { get; set; }

    /// <summary>Identificador de versión de la malla (p. ej. "2024-A").</summary>
    public string Version { get; set; } = string.Empty;

    public int Anio { get; set; }

    /// <summary>Fuente del plan (resolución, acta o documento institucional).</summary>
    public string Fuente { get; set; } = string.Empty;

    /// <summary>Ruta normativa aplicable: CAU-P-011 o CAU-P-122.</summary>
    public string? RutaNormativa { get; set; }

    public bool Vigente { get; set; } = true;

    public ICollection<Asignatura> Asignaturas { get; set; } = new List<Asignatura>();
}
