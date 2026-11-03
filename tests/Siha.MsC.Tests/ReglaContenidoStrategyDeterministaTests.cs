using FluentAssertions;
using Siha.MsC.Api.Domain;
using Siha.MsC.Api.Services;

namespace Siha.MsC.Tests;

/// <summary>
/// S2-S06 (US-10): pruebas deterministas de <see cref="ReglaContenidoStrategy"/> con vectores construidos a mano
/// (sin Ollama). Complementan ReglaContenidoStrategyTests con el borde exacto de τ, la ponderación por horas
/// con unidades de distinta duración, el plan sin horas, la invariancia frente al orden y a la escala de los
/// vectores, y la repetibilidad del resultado.
/// </summary>
public class ReglaContenidoStrategyDeterministaTests
{
    private readonly IReglaContenidoStrategy _strategy = new ReglaContenidoStrategy();

    /// <summary>16 unos: |a| = 4 exacto.</summary>
    private static float[] Referencia() => Enumerable.Repeat(1f, 16).ToArray();

    /// <summary>
    /// k unos seguidos de ceros: cos(Referencia, Parcial(k)) = k / (4·√k) = √k / 4.
    /// k = 9 → 0,75 exacto; k = 16 → 1; k = 4 → 0,5; k = 1 → 0,25.
    /// </summary>
    private static float[] Parcial(int k) => Enumerable.Range(0, 16).Select(i => i < k ? 1f : 0f).ToArray();

    /// <summary>Vector one-hot en la dimensión d (ortogonal a cualquier otro one-hot).</summary>
    private static float[] Eje(int d, int dim = 16) => Enumerable.Range(0, dim).Select(i => i == d ? 1f : 0f).ToArray();

    private static UnidadOrigenEmbedding O(int n, float[] v, int horas = 16) => new(n, $"Origen {n}", horas, $"Contenido origen {n}", v);
    private static UnidadDestinoEmbedding D(int n, float[] v, int horas = 16) => new(n, $"Destino {n}", horas, $"Contenido destino {n}", v);

    private EvaluacionContenido Calcular(IReadOnlyCollection<UnidadOrigenEmbedding> o, IReadOnlyCollection<UnidadDestinoEmbedding> d, double tau = 75)
        => _strategy.Calcular(new ParametrosContenido(o, d, tau, "CAU-P-011"));

    [Fact]
    public void Similitud_exactamente_igual_a_tau_cubre_la_unidad()
    {
        var r = Calcular(new[] { O(1, Referencia()) }, new[] { D(1, Parcial(9)) }, tau: 75);

        r.UnidadesCubiertas.Should().ContainSingle().Which.SimilitudMaxima.Should().Be(75.00);
        r.Pcont.Should().Be(100m, "la regla es s ≥ τ, no s > τ");
    }

    [Theory]
    [InlineData(75.0001)]
    [InlineData(75.01)]
    [InlineData(76)]
    public void Similitud_apenas_por_debajo_de_tau_no_cubre(double tau)
    {
        var r = Calcular(new[] { O(1, Referencia()) }, new[] { D(1, Parcial(9)) }, tau);

        r.UnidadesFaltantes.Should().ContainSingle().Which.SimilitudMaxima.Should().Be(75.00);
        r.Pcont.Should().Be(0m);
    }

    [Theory]
    [InlineData(16, 100.00)]
    [InlineData(9, 75.00)]
    [InlineData(4, 50.00)]
    [InlineData(1, 25.00)]
    public void La_similitud_reportada_es_el_coseno_exacto_en_porcentaje(int k, double esperado)
    {
        var r = Calcular(new[] { O(1, Referencia()) }, new[] { D(1, Parcial(k)) }, tau: 0);

        r.UnidadesCubiertas.Single().SimilitudMaxima.Should().Be(esperado);
    }

    [Fact]
    public void Seis_de_siete_con_horas_distintas_pondera_por_horas()
    {
        // Destino de 104 h: seis unidades de 16 h cubiertas y una de 8 h sin equivalente.
        var origen = Enumerable.Range(0, 6).Select(i => O(i + 1, Eje(i))).ToList();
        var destino = Enumerable.Range(0, 6).Select(i => D(i + 1, Eje(i), 16)).Append(D(7, Eje(15), 8)).ToList();

        var r = Calcular(origen, destino);

        r.UnidadesCubiertas.Should().HaveCount(6);
        r.UnidadesFaltantes.Should().ContainSingle(u => u.Numero == 7);
        r.Pcont.Should().Be(92.31m, "96 / 104 = 92,31 %, no 6/7 = 85,71 %");
    }

    [Fact]
    public void Seis_de_siete_faltando_una_unidad_grande_baja_de_85_71()
    {
        // Horas 8,8,8,8,8,8 cubiertas y 56 h sin equivalente: 48 / 104 = 46,15 %.
        var origen = Enumerable.Range(0, 6).Select(i => O(i + 1, Eje(i))).ToList();
        var destino = Enumerable.Range(0, 6).Select(i => D(i + 1, Eje(i), 8)).Append(D(7, Eje(15), 56)).ToList();

        Calcular(origen, destino).Pcont.Should().Be(46.15m);
    }

    [Fact]
    public void Seis_de_siete_con_horas_iguales_da_85_71_y_el_detalle_lo_explica()
    {
        var origen = Enumerable.Range(0, 6).Select(i => O(i + 1, Eje(i))).ToList();
        var destino = Enumerable.Range(0, 7).Select(i => D(i + 1, Eje(i), 16)).ToList();

        var r = Calcular(origen, destino);

        r.Pcont.Should().Be(85.71m);
        r.DetalleCalculo.Should().Contain("96 / 112").And.Contain("85.71");
    }

    [Fact]
    public void Plan_destino_sin_horas_da_Pcont_cero_sin_dividir_por_cero()
    {
        // Un plan analítico sin horas no puede ponderar: aunque las unidades coincidan, Pcont no sube.
        var r = Calcular(new[] { O(1, Eje(0)), O(2, Eje(1)) }, new[] { D(1, Eje(0), 0), D(2, Eje(1), 0) });

        r.Pcont.Should().Be(0m);
        r.UnidadesCubiertas.Should().HaveCount(2, "la similitud se informa igual, para que el docente la vea");
        r.DetalleCalculo.Should().Contain("0 / 0");
    }

    [Fact]
    public void Unidad_destino_sin_horas_no_pesa_en_el_resultado()
    {
        var r = Calcular(new[] { O(1, Eje(0)) }, new[] { D(1, Eje(0), 32), D(2, Eje(5), 0) });

        r.Pcont.Should().Be(100m);
        r.UnidadesFaltantes.Should().ContainSingle(u => u.Numero == 2);
    }

    [Fact]
    public void Vector_de_destino_en_ceros_no_produce_NaN_ni_cubre()
    {
        var r = Calcular(new[] { O(1, Referencia()) }, new[] { D(1, new float[16]) });

        r.UnidadesFaltantes.Single().SimilitudMaxima.Should().Be(0);
        double.IsNaN((double)r.Pcont).Should().BeFalse();
        r.Pcont.Should().Be(0m);
    }

    [Fact]
    public void Vectores_opuestos_no_dan_similitud_negativa()
    {
        var opuesto = Referencia().Select(x => -x).ToArray();

        var r = Calcular(new[] { O(1, Referencia()) }, new[] { D(1, opuesto) }, tau: 1);

        r.UnidadesFaltantes.Single().SimilitudMaxima.Should().Be(0);
    }

    [Fact]
    public void Escalar_los_vectores_no_cambia_el_resultado()
    {
        // Potencias de 2: el escalado es exacto en float, así se aísla la propiedad del coseno.
        var normal = Calcular(new[] { O(1, Referencia()) }, new[] { D(1, Parcial(9)), D(2, Parcial(4)) });
        var escalado = Calcular(
            new[] { O(1, Referencia().Select(x => x * 1024f).ToArray()) },
            new[] { D(1, Parcial(9).Select(x => x * 0.0009765625f).ToArray()), D(2, Parcial(4).Select(x => x * 8f).ToArray()) });

        escalado.Pcont.Should().Be(normal.Pcont);
        escalado.UnidadesCubiertas.Select(u => u.SimilitudMaxima).Should().Equal(normal.UnidadesCubiertas.Select(u => u.SimilitudMaxima));
    }

    [Fact]
    public void El_orden_de_las_unidades_no_cambia_Pcont_ni_el_desglose()
    {
        var origen = new[] { O(1, Eje(0)), O(2, Eje(1)), O(3, Eje(2)) };
        var destino = new[] { D(1, Eje(0), 16), D(2, Eje(1), 32), D(3, Eje(9), 48), D(4, Eje(2), 8) };

        var a = Calcular(origen, destino);
        var b = Calcular(origen.Reverse().ToArray(), destino.Reverse().ToArray());

        b.Pcont.Should().Be(a.Pcont);
        b.UnidadesCubiertas.Select(u => u.Numero).Should().Equal(a.UnidadesCubiertas.Select(u => u.Numero));
        b.UnidadesFaltantes.Select(u => u.Numero).Should().Equal(3);
        a.Pcont.Should().Be(53.85m, "(16 + 32 + 8) / 104");
    }

    [Fact]
    public void Cien_ejecuciones_del_mismo_caso_dan_exactamente_el_mismo_resultado()
    {
        var origen = new[] { O(1, Referencia()), O(2, Eje(3)) };
        var destino = new[] { D(1, Parcial(9)), D(2, Eje(3), 24), D(3, Parcial(4), 8) };
        var primero = Calcular(origen, destino);

        for (var i = 0; i < 100; i++)
        {
            var r = Calcular(origen, destino);
            r.Pcont.Should().Be(primero.Pcont);
            r.DetalleCalculo.Should().Be(primero.DetalleCalculo);
        }
    }

    [Fact]
    public void Pcont_se_redondea_a_dos_decimales()
    {
        // 1 de 3 unidades iguales: 33,333… → 33,33.
        var r = Calcular(new[] { O(1, Eje(0)) }, new[] { D(1, Eje(0)), D(2, Eje(1)), D(3, Eje(2)) });

        r.Pcont.Should().Be(33.33m);
    }

    [Fact]
    public void Tau_que_viene_de_gRPC_se_informa_tal_cual()
    {
        var r = Calcular(new[] { O(1, Referencia()) }, new[] { D(1, Parcial(9)) }, tau: 72.5);

        r.TauUsado.Should().Be(72.5);
        r.DetalleCalculo.Should().Contain("τ=72.5%");
    }
}
