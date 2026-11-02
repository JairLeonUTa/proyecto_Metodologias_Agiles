using Siha.MsC.Api.Domain;

namespace Siha.MsC.Api.Services;

/// <summary>
/// Implementación de la regla de proporción de créditos (Pcred) y controles normativos (US-09).
///
/// Reglas de negocio (CA1, CA2, CA3):
/// - Pcred = min(100, (credOrigen / credDestino) * 100). Se muestra SOLO como dato de apoyo.
/// - Denominador cero → error controlado (InvalidOperationException con mensaje claro).
/// - RN-03 (CAU-P-011): asignatura > 10 años → categoría VALIDACION_CONOCIMIENTOS.
/// - RN-16: 3ra matrícula en malla destino → BLOQUEO_3RA_MATRICULA (bloqueo duro).
/// - RN-15: créditos externos → conversión confirmada, NO aplicar 48h/crédito automático.
/// </summary>
public sealed class ReglaCreditosStrategy : IReglaCreditosStrategy
{
    private const int UmbralVigenciaAnos = 10;
    private const int MatriculaBloqueo = 3;
    private const string RutaCambioCarrera = "CAU-P-011";

    public EvaluacionCreditos Calcular(ParametrosCreditos p)
    {
        ValidarEntrada(p);

        var alertas = new List<string>();
        string categoria = "NORMAL";
        bool bloqueoDuro = false;

        // --- Pcred (CA1) ---
        decimal pcred = CalcularPcred(p.CreditosOrigen, p.CreditosDestino);

        // --- RN-03: Vigencia > 10 años (solo en ruta CAU-P-011) ---
        if (p.RutaNormativa == RutaCambioCarrera && p.AnosTranscurridos > UmbralVigenciaAnos)
        {
            categoria = "VALIDACION_CONOCIMIENTOS";
            alertas.Add($"Vigencia > {UmbralVigenciaAnos} años ({p.AnosTranscurridos} años): requiere validación de conocimientos (RN-03).");
        }

        // --- RN-16: 3ra matrícula en malla destino → BLOQUEO DURO ---
        if (p.NumeroMatriculaDestino >= MatriculaBloqueo)
        {
            categoria = "BLOQUEO_3RA_MATRICULA";
            bloqueoDuro = true;
            alertas.Add($"3ra matrícula detectada en malla destino (matrícula #{p.NumeroMatriculaDestino}): bloqueo por RN-16.");
        }

        // --- RN-15: Créditos externos ---
        if (p.EsCreditoExterno)
        {
            alertas.Add("Créditos externos detectados: conversión confirmada, NO aplicar 48h/crédito automático (RN-15).");
        }

        var detalle = $"Pcred = min(100, {p.CreditosOrigen} / {p.CreditosDestino} * 100) = {pcred:F2}%; " +
                      $"Ruta={p.RutaNormativa}; Años={p.AnosTranscurridos}; MatrículaDestino={p.NumeroMatriculaDestino}; " +
                      $"Externo={p.EsCreditoExterno}";

        return new EvaluacionCreditos(
            Pcred: pcred,
            Categoria: categoria,
            Alertas: alertas.AsReadOnly(),
            BloqueoDuro: bloqueoDuro,
            DetalleCalculo: detalle
        );
    }

    private static decimal CalcularPcred(int creditosOrigen, int creditosDestino)
    {
        var proporcion = (decimal)creditosOrigen / creditosDestino * 100m;
        return Math.Min(100m, Math.Round(proporcion, 2));
    }

    private static void ValidarEntrada(ParametrosCreditos p)
    {
        if (p.CreditosOrigen < 0)
            throw new ArgumentException("Los créditos de origen no pueden ser negativos.", nameof(p.CreditosOrigen));
        if (p.CreditosDestino <= 0)
            throw new InvalidOperationException("Los créditos de destino deben ser mayores a cero (denominador nulo).");
        if (p.AnosTranscurridos < 0)
            throw new ArgumentException("Los años transcurridos no pueden ser negativos.", nameof(p.AnosTranscurridos));
        if (p.NumeroMatriculaDestino < 1)
            throw new ArgumentException("El número de matrícula en destino debe ser >= 1.", nameof(p.NumeroMatriculaDestino));
        if (string.IsNullOrWhiteSpace(p.RutaNormativa))
            throw new ArgumentException("La ruta normativa es obligatoria.", nameof(p.RutaNormativa));
    }
}