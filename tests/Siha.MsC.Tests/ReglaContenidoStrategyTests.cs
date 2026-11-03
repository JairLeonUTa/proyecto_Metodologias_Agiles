using FluentAssertions;
using Siha.MsC.Api.Domain;
using Siha.MsC.Api.Services;

namespace Siha.MsC.Tests;

public class ReglaContenidoStrategyTests
{
    private readonly IReglaContenidoStrategy _strategy = new ReglaContenidoStrategy();

    private static float[] Vector(params float[] v) => v;

    private static UnidadOrigenEmbedding Origen(int numero, string nombre, int horas, params float[] emb)
        => new(numero, nombre, horas, $"Contenido {numero}", emb);

    private static UnidadDestinoEmbedding Destino(int numero, string nombre, int horas, params float[] emb)
        => new(numero, nombre, horas, $"Contenido {numero}", emb);

    private static ParametrosContenido CrearParametros(
        IReadOnlyCollection<UnidadOrigenEmbedding> origen,
        IReadOnlyCollection<UnidadDestinoEmbedding> destino,
        double tau = 75,
        string ruta = "CAU-P-011",
        bool validarRN19 = true)
        => new(origen, destino, tau, ruta, validarRN19);

    [Fact]
    public void Calcular_6_de_7_unidades_misma_duracion_da_85_71_porciento()
    {
        // 7 unidades destino de 1 hora cada una, 6 cubiertas, 1 no.
        // Usamos vectores ortogonales para las 6 primeras y un vector a 45° para D7
        // que da coseno ~0.707 < 0.75.
        var origen = new[]
        {
            Origen(1, "U1", 1, Vector(1, 0, 0, 0)),  // -> D1
            Origen(2, "U2", 1, Vector(0, 1, 0, 0)),  // -> D2
            Origen(3, "U3", 1, Vector(0, 0, 1, 0)),  // -> D3
            Origen(4, "U4", 1, Vector(0, 0, 0, 1)),  // -> D4
            Origen(5, "U5", 1, Vector(1, 1, 0, 0)),  // -> D5
            Origen(6, "U6", 1, Vector(0, 1, 1, 0)),  // -> D6
        };

        var destino = new[]
        {
            Destino(1, "D1", 1, Vector(1, 0, 0, 0)),
            Destino(2, "D2", 1, Vector(0, 1, 0, 0)),
            Destino(3, "D3", 1, Vector(0, 0, 1, 0)),
            Destino(4, "D4", 1, Vector(0, 0, 0, 1)),
            Destino(5, "D5", 1, Vector(1, 1, 0, 0)),
            Destino(6, "D6", 1, Vector(0, 1, 1, 0)),
            // D7 a 45° de todos: coseno = 1/√2 ≈ 0.707 < 0.75
            Destino(7, "D7", 1, Vector(1, 1, 1, 1)),
        };

        var r = _strategy.Calcular(CrearParametros(origen, destino));

        r.Pcont.Should().Be(85.71m); // 6/7 * 100
        r.UnidadesCubiertas.Should().HaveCount(6);
        r.UnidadesFaltantes.Should().HaveCount(1);
        r.UnidadesFaltantes.Single().Numero.Should().Be(7);
        r.TauUsado.Should().Be(75);
        r.ViolacionRN19.Should().BeFalse();
        r.UnidadesOrigenDuplicadas.Should().BeEmpty();
    }

    [Fact]
    public void Calcular_unidad_destino_sin_origen_similar_queda_faltante()
    {
        var origen = new[] { Origen(1, "U1", 1, Vector(1, 0, 0)) };
        var destino = new[]
        {
            Destino(1, "D1", 1, Vector(1, 0, 0)), // idéntica
            Destino(2, "D2", 1, Vector(0, 1, 0)), // ortogonal a U1 -> similitud 0
        };

        var r = _strategy.Calcular(CrearParametros(origen, destino));

        r.Pcont.Should().Be(50m); // 1/2
        r.UnidadesCubiertas.Should().HaveCount(1);
        r.UnidadesFaltantes.Should().HaveCount(1);
        r.UnidadesFaltantes.Single().Numero.Should().Be(2);
    }

    [Fact]
    public void Calcular_N_a_1_varias_origen_misma_destino_toma_max_similitud()
    {
        // Dos unidades origen cubren la misma destino (N:1)
        var origen = new[]
        {
            Origen(1, "Fisica I", 1, Vector(1, 0, 0)),
            Origen(2, "Fisica II", 1, Vector(0.8f, 0.6f, 0)), // coseno ~0.8 con D1
        };
        var destino = new[] { Destino(1, "Fisica General", 1, Vector(1, 0, 0)) };

        var r = _strategy.Calcular(CrearParametros(origen, destino));

        r.Pcont.Should().Be(100m);
        r.UnidadesCubiertas.Single().UnidadesOrigenContribuyentes.Should().Contain(1);
    }

    [Fact]
    public void Calcular_tau_0_todo_cubierto()
    {
        var origen = new[] { Origen(1, "U1", 1, Vector(1, 0, 0)) };
        var destino = new[] { Destino(1, "D1", 1, Vector(0, 1, 0)) }; // ortogonal

        var r = _strategy.Calcular(CrearParametros(origen, destino, tau: 0));

        r.Pcont.Should().Be(100m);
        r.UnidadesCubiertas.Should().HaveCount(1);
    }

    [Fact]
    public void Calcular_tau_100_nada_cubierto_salvo_idénticos()
    {
        var origen = new[] { Origen(1, "U1", 1, Vector(1, 0, 0)) };
        var destino = new[]
        {
            Destino(1, "D1", 1, Vector(1, 0, 0)), // idéntica -> coseno = 1
            Destino(2, "D2", 1, Vector(0.9f, 0.1f, 0)), // coseno < 1
        };

        var r = _strategy.Calcular(CrearParametros(origen, destino, tau: 100));

        r.Pcont.Should().Be(50m); // solo D1 cubierta (similitud 100%)
        r.UnidadesCubiertas.Single().Numero.Should().Be(1);
        r.UnidadesFaltantes.Single().Numero.Should().Be(2);
    }

    [Fact]
    public void Calcular_ponderado_por_horas_unidad_grande_pesa_mas()
    {
        var origen = new[] { Origen(1, "U1", 1, Vector(1, 0, 0)) };
        var destino = new[]
        {
            Destino(1, "D1", 5, Vector(1, 0, 0)), // 5h, cubierta
            Destino(2, "D2", 1, Vector(0, 1, 0)), // 1h, no cubierta
        };

        var r = _strategy.Calcular(CrearParametros(origen, destino));

        // 5h cubiertas de 6h totales = 83.33%
        r.Pcont.Should().Be(83.33m);
    }

    [Fact]
    public void Calcular_embedding_nulo_o_dimension_distinta_lanza_error()
    {
        var origen = new[] { Origen(1, "U1", 1, Vector(1, 0)) }; // dim 2
        var destino = new[] { Destino(1, "D1", 1, Vector(1, 0, 0)) }; // dim 3

        var act = () => _strategy.Calcular(CrearParametros(origen, destino));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*misma dimensión*");
    }

    [Fact]
    public void Calcular_sin_unidades_origen_lanza_error()
    {
        var act = () => _strategy.Calcular(CrearParametros(
            Array.Empty<UnidadOrigenEmbedding>(),
            new[] { Destino(1, "D1", 1, Vector(1, 0, 0)) }));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*al menos una unidad de origen*");
    }

    [Fact]
    public void Calcular_sin_unidades_destino_lanza_error()
    {
        var act = () => _strategy.Calcular(CrearParametros(
            new[] { Origen(1, "U1", 1, Vector(1, 0, 0)) },
            Array.Empty<UnidadDestinoEmbedding>()));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*al menos una unidad de destino*");
    }

    [Fact]
    public void Calcular_tau_fuera_de_rango_lanza_error()
    {
        var act = () => _strategy.Calcular(CrearParametros(
            new[] { Origen(1, "U1", 1, Vector(1, 0, 0)) },
            new[] { Destino(1, "D1", 1, Vector(1, 0, 0)) },
            tau: 150));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*tau*");
    }

    [Fact]
    public void Calcular_detalle_incluye_trazabilidad_completa()
    {
        var origen = new[] { Origen(1, "U1", 1, Vector(1, 0, 0)) };
        var destino = new[] { Destino(1, "D1", 1, Vector(1, 0, 0)) };

        var r = _strategy.Calcular(CrearParametros(origen, destino));

        r.DetalleCalculo.Should().Contain("Pcont");
        r.DetalleCalculo.Should().Contain("τ=75%");
        r.DetalleCalculo.Should().Contain("CAU-P-011");
    }

    // --- RN-19 tests (S2-J07) ---

    [Fact]
    public void Calcular_RN19_unidad_origen_en_dos_destinos_cubiertos_viola_regla()
    {
        // U1 cubre D1 y D2 (ambos idénticos a U1) -> RN-19 violado
        var origen = new[] { Origen(1, "U1", 1, Vector(1, 0, 0)) };
        var destino = new[]
        {
            Destino(1, "D1", 1, Vector(1, 0, 0)), // idéntica
            Destino(2, "D2", 1, Vector(1, 0, 0)), // idéntica -> U1 usada dos veces
        };

        var r = _strategy.Calcular(CrearParametros(origen, destino));

        r.ViolacionRN19.Should().BeTrue();
        r.UnidadesOrigenDuplicadas.Should().ContainSingle(x => x == 1);
        r.DetalleCalculo.Should().Contain("RN-19 VIOLADO");
    }

    [Fact]
    public void Calcular_RN19_unidad_origen_en_tres_destinos_cubiertos_viola_regla()
    {
        var origen = new[] { Origen(1, "U1", 1, Vector(1, 0, 0)) };
        var destino = new[]
        {
            Destino(1, "D1", 1, Vector(1, 0, 0)),
            Destino(2, "D2", 1, Vector(1, 0, 0)),
            Destino(3, "D3", 1, Vector(1, 0, 0)),
        };

        var r = _strategy.Calcular(CrearParametros(origen, destino));

        r.ViolacionRN19.Should().BeTrue();
        r.UnidadesOrigenDuplicadas.Should().ContainSingle(x => x == 1);
    }

    [Fact]
    public void Calcular_RN19_unidad_origen_en_destino_cubierto_y_otro_no_cubierto_no_viola()
    {
        // U1 cubre D1 (idéntica), D2 no cubierta (similitud 0) -> OK
        var origen = new[] { Origen(1, "U1", 1, Vector(1, 0, 0)) };
        var destino = new[]
        {
            Destino(1, "D1", 1, Vector(1, 0, 0)), // cubierta
            Destino(2, "D2", 1, Vector(0, 1, 0)), // NO cubierta (ortogonal)
        };

        var r = _strategy.Calcular(CrearParametros(origen, destino));

        r.ViolacionRN19.Should().BeFalse();
        r.UnidadesOrigenDuplicadas.Should().BeEmpty();
    }

    [Fact]
    public void Calcular_RN19_dos_unidades_origen_cada_una_en_un_destino_OK()
    {
        var origen = new[]
        {
            Origen(1, "U1", 1, Vector(1, 0, 0)),
            Origen(2, "U2", 1, Vector(0, 1, 0)),
        };
        var destino = new[]
        {
            Destino(1, "D1", 1, Vector(1, 0, 0)), // U1
            Destino(2, "D2", 1, Vector(0, 1, 0)), // U2
        };

        var r = _strategy.Calcular(CrearParametros(origen, destino));

        r.ViolacionRN19.Should().BeFalse();
        r.UnidadesOrigenDuplicadas.Should().BeEmpty();
    }

    [Fact]
    public void Calcular_RN19_deshabilitado_no_reporta_violacion()
    {
        var origen = new[] { Origen(1, "U1", 1, Vector(1, 0, 0)) };
        var destino = new[]
        {
            Destino(1, "D1", 1, Vector(1, 0, 0)),
            Destino(2, "D2", 1, Vector(1, 0, 0)),
        };

        var r = _strategy.Calcular(CrearParametros(origen, destino, validarRN19: false));

        r.ViolacionRN19.Should().BeFalse("ValidarRN19=false desactiva la comprobación");
        r.UnidadesOrigenDuplicadas.Should().BeEmpty();
    }

    [Fact]
    public void Calcular_N_a_1_con_RN19_OK_si_origenes_distintos_para_distintos_destinos()
    {
        // Fisica I (U1) -> Fisica General (D1)
        // Fisica II (U2) -> Fisica General (D1)  -- N:1 mismo destino, OK
        // Quimica (U3) -> Quimica General (D2)   -- origen distinto, OK
        var origen = new[]
        {
            Origen(1, "Fisica I", 1, Vector(1, 0, 0)),
            Origen(2, "Fisica II", 1, Vector(0.8f, 0.6f, 0)),
            Origen(3, "Quimica", 1, Vector(0, 0, 1)),
        };
        var destino = new[]
        {
            Destino(1, "Fisica General", 1, Vector(1, 0, 0)), // mejor match U1
            Destino(2, "Quimica General", 1, Vector(0, 0, 1)), // match U3
        };

        var r = _strategy.Calcular(CrearParametros(origen, destino));

        r.ViolacionRN19.Should().BeFalse();
        r.UnidadesCubiertas.Should().HaveCount(2);
        // D1 tiene contribuyentes U1 (y U2 por empate técnico cercano)
        r.UnidadesCubiertas.Single(c => c.Numero == 1).UnidadesOrigenContribuyentes.Should().Contain(1);
    }
}