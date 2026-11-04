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

    /// <summary>Semestre en el que se cursó la asignatura de origen (certificado SIHA).</summary>
    public int? Semestre { get; set; }

    /// <summary>Créditos de la asignatura de origen (certificado SIHA).</summary>
    public int? Creditos { get; set; }

    /// <summary>Horas totales de la asignatura de origen (certificado SIHA).</summary>
    public int? Horas { get; set; }
}
