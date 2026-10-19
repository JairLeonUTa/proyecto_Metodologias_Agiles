namespace Siha.MsA.Api.Domain.Entities;

/// <summary>
/// Parámetros heurísticos de evaluación (US-02): θ (theta), δ (delta) y τ (tau), expresados en
/// porcentaje (0-100). Se guardan por ruta normativa y versión. Invariante de validación: δ ≤ θ.
/// </summary>
public class ParametroEvaluacion
{
    public int Id { get; set; }

    /// <summary>Ruta normativa: CAU-P-011 o CAU-P-122.</summary>
    public string RutaNormativa { get; set; } = string.Empty;

    /// <summary>θ: umbral de similitud global (%) en [0, 100].</summary>
    public double Theta { get; set; }

    /// <summary>δ: banda de tolerancia (%) en [0, 100]; debe cumplir δ ≤ θ.</summary>
    public double Delta { get; set; }

    /// <summary>τ: umbral de cobertura temática (%) en [0, 100].</summary>
    public double Tau { get; set; }

    /// <summary>Versión de la configuración de parámetros.</summary>
    public int Version { get; set; } = 1;

    public DateTimeOffset ActualizadoUtc { get; set; } = DateTimeOffset.UtcNow;

    public string ActualizadoPor { get; set; } = string.Empty;
}
