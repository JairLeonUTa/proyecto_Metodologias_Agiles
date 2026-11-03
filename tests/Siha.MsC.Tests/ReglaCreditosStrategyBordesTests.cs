using FluentAssertions;
using Siha.MsC.Api.Domain;
using Siha.MsC.Api.Services;

namespace Siha.MsC.Tests;

/// <summary>
/// S2-S04 (US-09): suite TDD de casos borde de <see cref="ReglaCreditosStrategy"/>.
/// Complementa ReglaCreditosStrategyTests con redondeo, créditos cero, créditos externos
/// conservados, unión N:1 (prepara US-10) y combinaciones de controles normativos.
/// </summary>
public class ReglaCreditosStrategyBordesTests
{
    private const string CambioCarrera = "CAU-P-011";
    private const string Movilidad = "CAU-P-122";

    private readonly IReglaCreditosStrategy _strategy = new ReglaCreditosStrategy();

    private EvaluacionCreditos Calcular(
        int origen, int destino, int anos = 2, int matricula = 1, bool externo = false, string ruta = CambioCarrera)
        => _strategy.Calcular(new ParametrosCreditos(origen, destino, anos, matricula, externo, ruta));

    [Theory]
    [InlineData(1, 3, 33.33)]
    [InlineData(2, 3, 66.67)]
    [InlineData(2, 7, 28.57)]
    [InlineData(3, 4, 75.00)]
    [InlineData(4, 5, 80.00)]
    [InlineData(3, 3, 100.00)]
    public void Pcred_se_redondea_a_dos_decimales(int origen, int destino, double esperado)
    {
        Calcular(origen, destino).Pcred.Should().Be((decimal)esperado);
    }

    [Fact]
    public void Creditos_de_origen_cero_dan_0_por_ciento_sin_error()
    {
        var r = Calcular(0, 3);

        r.Pcred.Should().Be(0m);
        r.Categoria.Should().Be("NORMAL");
        r.BloqueoDuro.Should().BeFalse();
    }

    [Fact]
    public void Creditos_muy_grandes_no_desbordan_y_respetan_el_tope_de_100()
    {
        var r = Calcular(int.MaxValue, 1);

        r.Pcred.Should().Be(100m);
    }

    [Fact]
    public void Creditos_de_destino_negativos_se_tratan_como_denominador_invalido()
    {
        var accion = () => Calcular(3, -2);

        accion.Should().Throw<InvalidOperationException>()
            .WithMessage("*denominador nulo*");
    }

    [Fact]
    public void Creditos_externos_se_conservan_sin_convertir_a_horas()
    {
        // RN-15: 5 créditos externos frente a 4 de destino. El cálculo usa el valor confirmado (5),
        // no una conversión automática de 48 h por crédito.
        var r = Calcular(5, 4, externo: true);

        r.Pcred.Should().Be(100m);
        r.DetalleCalculo.Should().Contain("5 / 4").And.Contain("Externo=True");
        r.DetalleCalculo.Should().NotContain("48");
        r.Alertas.Should().ContainSingle(a => a.Contains("RN-15"));
    }

    [Theory]
    [InlineData(new[] { 2, 2 }, 3, 100.00)] // Física I + Física II → Física
    [InlineData(new[] { 1, 1 }, 3, 66.67)]
    [InlineData(new[] { 1, 1, 1 }, 4, 75.00)]
    public void Union_N_a_1_suma_los_creditos_de_origen_antes_de_calcular(int[] origenes, int destino, double esperado)
    {
        // Prepara US-10: varias asignaturas de origen hacia una de destino se evalúan con la suma.
        var r = Calcular(origenes.Sum(), destino);

        r.Pcred.Should().Be((decimal)esperado);
    }

    [Fact]
    public void Cuarta_matricula_tambien_bloquea()
    {
        var r = Calcular(3, 3, matricula: 4);

        r.BloqueoDuro.Should().BeTrue();
        r.Categoria.Should().Be("BLOQUEO_3RA_MATRICULA");
    }

    [Fact]
    public void Tercera_matricula_con_credito_externo_bloquea_y_conserva_ambas_alertas()
    {
        var r = Calcular(3, 3, matricula: 3, externo: true);

        r.BloqueoDuro.Should().BeTrue();
        r.Alertas.Should().HaveCount(2);
        r.Alertas.Should().Contain(a => a.Contains("RN-16"));
        r.Alertas.Should().Contain(a => a.Contains("RN-15"));
    }

    [Fact]
    public void Movilidad_con_mas_de_10_anos_y_externo_no_deriva_a_validacion()
    {
        var r = Calcular(3, 3, anos: 15, externo: true, ruta: Movilidad);

        r.Categoria.Should().Be("NORMAL");
        r.BloqueoDuro.Should().BeFalse();
        r.Alertas.Should().ContainSingle(a => a.Contains("RN-15"));
    }

    [Fact]
    public void Once_anos_en_segunda_matricula_deriva_a_validacion_sin_bloqueo()
    {
        var r = Calcular(3, 3, anos: 11, matricula: 2);

        r.Categoria.Should().Be("VALIDACION_CONOCIMIENTOS");
        r.BloqueoDuro.Should().BeFalse("la validación de conocimientos no es un bloqueo duro");
    }

    [Fact]
    public void El_bloqueo_por_matricula_no_cambia_el_Pcred()
    {
        var normal = Calcular(2, 3);
        var bloqueado = Calcular(2, 3, matricula: 3);

        bloqueado.Pcred.Should().Be(normal.Pcred, "Pcred es un dato de apoyo independiente de los controles");
    }

    [Fact]
    public void Mismo_caso_dos_veces_da_el_mismo_resultado()
    {
        var a = Calcular(4, 3, anos: 12, matricula: 1, externo: true);
        var b = Calcular(4, 3, anos: 12, matricula: 1, externo: true);

        b.Should().BeEquivalentTo(a);
    }

    [Fact]
    public void La_ruta_se_compara_exacta_y_no_acepta_variantes()
    {
        // Una ruta escrita distinto no activa la regla de 10 años: se documenta el comportamiento actual.
        var r = Calcular(3, 3, anos: 15, ruta: "cau-p-011");

        r.Categoria.Should().Be("NORMAL");
    }
}
