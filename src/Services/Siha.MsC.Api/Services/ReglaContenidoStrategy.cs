using System.Linq;
using Siha.MsC.Api.Domain;

namespace Siha.MsC.Api.Services;

/// <summary>
/// Implementación de la regla de similitud semántica de contenidos (US-10, S2-J06).
///
/// Algoritmo:
///   sj = max_u cos(t_j, u)   — mejor similitud coseno entre unidad destino j y cualquier unidad origen u
///   cubierta si sj ≥ τ (tau, ej. 75%)
///   Pcont = Σ(h_j * covered_j) / Σ(h_j) * 100  — ponderado por horas
///
/// RN-19: no reutilizar materia origen en dos destinos del mismo expediente (se controla en orquestador).
/// N:1: varias unidades origen → una destino (unión matemática: se toma el max de similitud).
/// </summary>
public sealed class ReglaContenidoStrategy : IReglaContenidoStrategy
{
    public EvaluacionContenido Calcular(ParametrosContenido p)
    {
        ValidarEntrada(p);

        var tauNormalizado = p.Tau / 100.0; // τ viene en porcentaje (ej. 75), coseno está en [0,1]

        var cubiertas = new List<UnidadCobertura>();
        var faltantes = new List<UnidadCobertura>();
        double sumaHorasPesoCubiertas = 0;
        double sumaHorasTotales = 0;

        foreach (var destino in p.UnidadesDestino.OrderBy(u => u.Numero))
        {
            sumaHorasTotales += destino.Horas;

            // Buscar la mejor similitud coseno contra TODAS las unidades de origen (N:1)
            double mejorSimilitud = 0;
            var contribuyentes = new List<int>();

            foreach (var origen in p.UnidadesOrigen)
            {
                var sim = SimilitudCoseno(origen.Embedding, destino.Embedding);
                if (sim > mejorSimilitud)
                {
                    mejorSimilitud = sim;
                    contribuyentes.Clear();
                    contribuyentes.Add(origen.Numero);
                }
                else if (Math.Abs(sim - mejorSimilitud) < 1e-9 && sim > 0)
                {
                    // Empate técnico: agregar también
                    if (!contribuyentes.Contains(origen.Numero))
                        contribuyentes.Add(origen.Numero);
                }
            }

            var cubierta = mejorSimilitud >= tauNormalizado;
            var unidad = new UnidadCobertura(
                Numero: destino.Numero,
                Nombre: destino.Nombre,
                Horas: destino.Horas,
                SimilitudMaxima: Math.Round(mejorSimilitud * 100, 2), // guardar en %
                Cubierta: cubierta,
                UnidadesOrigenContribuyentes: contribuyentes.AsReadOnly()
            );

            if (cubierta)
            {
                cubiertas.Add(unidad);
                sumaHorasPesoCubiertas += destino.Horas;
            }
            else
            {
                faltantes.Add(unidad);
            }
        }

        var pcont = sumaHorasTotales > 0
            ? (decimal)Math.Round(sumaHorasPesoCubiertas / sumaHorasTotales * 100, 2)
            : 0m;

        var detalle = $"Pcont = Σ(h_j * covered_j) / Σ(h_j) * 100 = {sumaHorasPesoCubiertas:F0} / {sumaHorasTotales:F0} * 100 = {pcont}%; " +
                      $"τ={p.Tau}%; UnidadesDestino={p.UnidadesDestino.Count}; UnidadesOrigen={p.UnidadesOrigen.Count}; " +
                      $"Ruta={p.RutaNormativa}";

        return new EvaluacionContenido(
            Pcont: (decimal)pcont,
            UnidadesCubiertas: cubiertas.AsReadOnly(),
            UnidadesFaltantes: faltantes.AsReadOnly(),
            TauUsado: p.Tau,
            DetalleCalculo: detalle
        );
    }

    /// <summary>Similitud coseno entre dos vectores: cos(θ) = (A·B) / (||A|| * ||B||).</summary>
    private static double SimilitudCoseno(float[] a, float[] b)
    {
        if (a is null || b is null || a.Length == 0 || b.Length != a.Length)
            return 0.0;

        double dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denom == 0 ? 0.0 : dot / denom;
    }

    private static void ValidarEntrada(ParametrosContenido p)
    {
        if (p.UnidadesOrigen is null || p.UnidadesOrigen.Count == 0)
            throw new ArgumentException("Debe haber al menos una unidad de origen.", nameof(p.UnidadesOrigen));
        if (p.UnidadesDestino is null || p.UnidadesDestino.Count == 0)
            throw new ArgumentException("Debe haber al menos una unidad de destino.", nameof(p.UnidadesDestino));
        if (p.Tau < 0 || p.Tau > 100)
            throw new ArgumentException("El umbral τ (tau) debe estar en [0, 100].", nameof(p.Tau));
        if (string.IsNullOrWhiteSpace(p.RutaNormativa))
            throw new ArgumentException("La ruta normativa es obligatoria.", nameof(p.RutaNormativa));

        // Verificar que todos los embeddings tengan la misma dimensión
        var dimOrigen = p.UnidadesOrigen.First().Embedding?.Length ?? 0;
        if (dimOrigen == 0)
            throw new ArgumentException("Las unidades de origen deben tener embeddings válidos.", nameof(p.UnidadesOrigen));
        if (p.UnidadesOrigen.Any(u => u.Embedding?.Length != dimOrigen))
            throw new ArgumentException("Todas las unidades de origen deben tener embeddings de la misma dimensión.", nameof(p.UnidadesOrigen));
        if (p.UnidadesDestino.Any(u => u.Embedding?.Length != dimOrigen))
            throw new ArgumentException("Las unidades de destino deben tener embeddings de la misma dimensión que las de origen.", nameof(p.UnidadesDestino));
    }
}