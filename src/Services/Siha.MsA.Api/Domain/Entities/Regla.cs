namespace Siha.MsA.Api.Domain.Entities;

/// <summary>
/// Regla de negocio configurable (US-02). Cada regla se guarda con su tipo de trámite, versión,
/// fuente y vigencia. Los umbrales normativos CAU-P-011 / CAU-P-122 se mantienen separados de los
/// parámetros heurísticos θ, δ, τ (ver <see cref="ParametroEvaluacion"/>).
/// </summary>
public class Regla
{
    public int Id { get; set; }

    /// <summary>Código de la regla (p. ej. RN-01 ... RN-19).</summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>Ruta normativa a la que aplica: CAU-P-011 o CAU-P-122.</summary>
    public string RutaNormativa { get; set; } = string.Empty;

    /// <summary>Tipo de trámite: INTERNO o EXTERNO.</summary>
    public string TipoTramite { get; set; } = string.Empty;

    public string Descripcion { get; set; } = string.Empty;

    /// <summary>Expresión/valor de configuración de la regla.</summary>
    public string? Expresion { get; set; }

    /// <summary>Versión de la regla.</summary>
    public string Version { get; set; } = "1.0";

    /// <summary>Fuente normativa (p. ej. "Resolución CAU-P-011").</summary>
    public string Fuente { get; set; } = string.Empty;

    /// <summary>Verdadero para los umbrales normativos de las resoluciones CAU (no heurísticos).</summary>
    public bool EsNormativa { get; set; }

    public DateOnly VigenteDesde { get; set; }

    public DateOnly? VigenteHasta { get; set; }

    public bool Activa { get; set; } = true;
}
