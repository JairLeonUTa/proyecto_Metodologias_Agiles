namespace Siha.MsA.Api.Services;

/// <summary>
/// Validador de los parámetros heurísticos de evaluación (US-02, CA2).
/// Reglas: cada valor en [0, 100] y la banda de tolerancia no puede superar al umbral (δ ≤ θ).
/// </summary>
public static class ParametrosValidator
{
    public const double Minimo = 0d;
    public const double Maximo = 100d;

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

        if (delta > theta)
        {
            return (false, "La banda de tolerancia δ no puede ser mayor que el umbral θ (δ ≤ θ).");
        }

        return (true, null);
    }
}
