namespace Siha.MsA.Api.Services;

/// <summary>
/// Validador de los parámetros heurísticos de evaluación (US-02, CA2).
/// Reglas: cada valor en [0, 100], la banda de tolerancia no puede superar al umbral (δ ≤ θ) y el
/// contenido mínimo θ nunca puede ser menor que el 80 % que fija el reglamento de homologación de la UTA
/// (se puede exigir más, nunca menos).
/// </summary>
public static class ParametrosValidator
{
    public const double Minimo = 0d;
    public const double Maximo = 100d;

    /// <summary>Contenido mínimo que fija el reglamento: el administrador no puede bajarlo.</summary>
    public const double ThetaMinimoReglamento = 80d;

    public static (bool Valido, string? Error) Validar(double theta, double delta, double tau)
    {
        if (double.IsNaN(theta) || double.IsNaN(delta) || double.IsNaN(tau)
            || double.IsInfinity(theta) || double.IsInfinity(delta) || double.IsInfinity(tau))
        {
            return (false, "Los parámetros θ, δ y τ deben ser valores numéricos finitos.");
        }

        if (theta < Minimo || theta > Maximo)
        {
            return (false, "El umbral θ debe estar en el rango [0, 100].");
        }

        if (delta < Minimo || delta > Maximo)
        {
            return (false, "La banda δ debe estar en el rango [0, 100].");
        }

        if (tau < Minimo || tau > Maximo)
        {
            return (false, "El umbral τ debe estar en el rango [0, 100].");
        }

        if (theta < ThetaMinimoReglamento)
        {
            return (false, $"El contenido mínimo no puede ser menor que {ThetaMinimoReglamento:0} %: lo fija el reglamento de homologación de la UTA. Puede exigir más, pero no menos.");
        }

        if (delta > theta)
        {
            return (false, "La banda de tolerancia δ no puede ser mayor que el umbral θ (δ ≤ θ).");
        }

        return (true, null);
    }
}
