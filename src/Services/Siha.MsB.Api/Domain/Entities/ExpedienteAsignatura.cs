namespace Siha.MsB.Api.Domain.Entities;

/// <summary>Asignatura de origen declarada en el expediente, conservando su referencia de catálogo.</summary>
public class ExpedienteAsignatura
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ExpedienteId { get; set; }
    public Expediente? Expediente { get; set; }

    public string AsignaturaOrigenCodigo { get; set; } = string.Empty;

    public string AsignaturaOrigenNombre { get; set; } = string.Empty;

    public string? AsignaturaDestinoCodigo { get; set; }

    public double? NotaOrigen { get; set; }

    /// <summary>
    /// Fecha en la que la institución de origen certificó la aprobación de la asignatura.
    /// Es un requisito normativo (RN-03, CAU-P-122) y solo se informa para materias aprobadas
    /// y certificadas seleccionadas del catálogo.
    /// </summary>
    public DateOnly? FechaAprobacionCertificada { get; set; }

    /// <summary>Número de matrícula de la persona en la asignatura de destino, si consta en el expediente.</summary>
    public int? NumeroMatriculaDestino { get; set; }

    /// <summary>Nivel (semestre) de la carrera de origen en que se cursó (certificado SIHA).</summary>
    public int? Nivel { get; set; }

    /// <summary>Período académico en que se cursó, por ejemplo "Abr-Sep 2025" (certificado SIHA).</summary>
    public string? PeriodoAcademico { get; set; }

    /// <summary>True si la aprobó; false si la reprobó (el certificado trae ambas).</summary>
    public bool? Aprobada { get; set; }

    /// <summary>Materia de la carrera de destino por la que pide homologarla (nombre completo).</summary>
    public string? AsignaturaDestinoNombre { get; set; }

    /// <summary>Número de la solicitud del certificado (varias materias pueden ir en la misma).</summary>
    public int? SolicitudNumero { get; set; }
}
