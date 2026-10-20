namespace Siha.MsA.Api.Domain.Entities;

/// <summary>Asignatura de una malla curricular con créditos, horas y unidades temáticas.</summary>
public class Asignatura
{
    public int Id { get; set; }

    public int MallaId { get; set; }
    public MallaCurricular? Malla { get; set; }

    /// <summary>Código único dentro de la malla (los duplicados se rechazan).</summary>
    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public int Creditos { get; set; }

    /// <summary>Horas totales; equivalencia institucional UTA de 48 h por crédito.</summary>
    public int Horas { get; set; }

    public int Semestre { get; set; }

    /// <summary>Falso cuando el plan analítico llega incompleto y no debe pasar a análisis semántico.</summary>
    public bool PlanCompleto { get; set; } = true;

    public bool Activa { get; set; } = true;

    public ICollection<UnidadTematica> Unidades { get; set; } = new List<UnidadTematica>();
}
