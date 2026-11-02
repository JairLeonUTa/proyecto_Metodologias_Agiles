using FluentAssertions;
using Siha.MsC.Api.Domain;
using Siha.MsC.Api.Services;

namespace Siha.MsC.Tests;

public class ReglaCreditosStrategyTests
{
    private readonly IReglaCreditosStrategy _strategy = new ReglaCreditosStrategy();

    [Fact]
    public void Calcular_4_vs_3_retorna_100_porciento()
    {
        var p = new ParametrosCreditos(4, 3, 2, 1, false, "CAU-P-011");
        var r = _strategy.Calcular(p);

        r.Pcred.Should().Be(100m);
        r.Categoria.Should().Be("NORMAL");
        r.BloqueoDuro.Should().BeFalse();
    }

    [Fact]
    public void Calcular_2_vs_3_retorna_66_67_porciento()
    {
        var p = new ParametrosCreditos(2, 3, 2, 1, false, "CAU-P-011");
        var r = _strategy.Calcular(p);

        r.Pcred.Should().Be(66.67m);
    }

    [Fact]
    public void Calcular_creditos_origen_mayor_a_destino_tope_100()
    {
        var p = new ParametrosCreditos(5, 3, 2, 1, false, "CAU-P-011");
        var r = _strategy.Calcular(p);

        r.Pcred.Should().Be(100m);
    }

    [Fact]
    public void Calcular_denominador_cero_lanza_excepcion_controlada()
    {
        var p = new ParametrosCreditos(3, 0, 2, 1, false, "CAU-P-011");
        var act = () => _strategy.Calcular(p);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*denominador nulo*");
    }

    [Fact]
    public void Calcular_mas_de_10_anos_ruta_cau_p_011_categoria_validacion_conocimientos()
    {
        var p = new ParametrosCreditos(3, 3, 11, 1, false, "CAU-P-011");
        var r = _strategy.Calcular(p);

        r.Categoria.Should().Be("VALIDACION_CONOCIMIENTOS");
        r.Alertas.Should().Contain(a => a.Contains("VALIDACION_CONOCIMIENTOS") || a.Contains("RN-03"));
        r.BloqueoDuro.Should().BeFalse();
    }

    [Fact]
    public void Calcular_10_anos_exactos_no_dispara_validacion_conocimientos()
    {
        var p = new ParametrosCreditos(3, 3, 10, 1, false, "CAU-P-011");
        var r = _strategy.Calcular(p);

        r.Categoria.Should().Be("NORMAL");
        r.Alertas.Should().NotContain(a => a.Contains("VALIDACION_CONOCIMIENTOS"));
    }

    [Fact]
    public void Calcular_ruta_distinta_a_cau_p_011_no_aplica_vigencia_10_anos()
    {
        var p = new ParametrosCreditos(3, 3, 15, 1, false, "CAU-P-122");
        var r = _strategy.Calcular(p);

        r.Categoria.Should().Be("NORMAL");
        r.Alertas.Should().NotContain(a => a.Contains("VALIDACION_CONOCIMIENTOS"));
    }

    [Fact]
    public void Calcular_tercera_matricula_bloqueo_duro_RN16()
    {
        var p = new ParametrosCreditos(3, 3, 2, 3, false, "CAU-P-011");
        var r = _strategy.Calcular(p);

        r.Categoria.Should().Be("BLOQUEO_3RA_MATRICULA");
        r.BloqueoDuro.Should().BeTrue();
        r.Alertas.Should().Contain(a => a.Contains("RN-16") || a.Contains("3ra matrícula"));
    }

    [Fact]
    public void Calcular_segunda_matricula_no_bloquea()
    {
        var p = new ParametrosCreditos(3, 3, 2, 2, false, "CAU-P-011");
        var r = _strategy.Calcular(p);

        r.BloqueoDuro.Should().BeFalse();
        r.Categoria.Should().NotBe("BLOQUEO_3RA_MATRICULA");
    }

    [Fact]
    public void Calcular_credito_externo_alerta_RN15_sin_bloqueo()
    {
        var p = new ParametrosCreditos(3, 3, 2, 1, true, "CAU-P-011");
        var r = _strategy.Calcular(p);

        r.Alertas.Should().Contain(a => a.Contains("RN-15") || a.Contains("48h"));
        r.BloqueoDuro.Should().BeFalse();
    }

    [Fact]
    public void Calcular_combinado_vigencia_y_tercera_matricula_bloqueo_prioritario()
    {
        var p = new ParametrosCreditos(3, 3, 15, 3, false, "CAU-P-011");
        var r = _strategy.Calcular(p);

        r.Categoria.Should().Be("BLOQUEO_3RA_MATRICULA");
        r.BloqueoDuro.Should().BeTrue();
    }

    [Theory]
    [InlineData(-1, 3, 2, 1, "CreditosOrigen")]
    [InlineData(3, 3, -1, 1, "AnosTranscurridos")]
    [InlineData(3, 3, 2, 0, "NumeroMatriculaDestino")]
    public void Calcular_parametros_invalidos_lanza_argumentexception(int credOrigen, int credDestino, int anos, int matricula, string parametroEsperado)
    {
        var p = new ParametrosCreditos(credOrigen, credDestino, anos, matricula, false, "CAU-P-011");
        var act = () => _strategy.Calcular(p);

        act.Should().Throw<ArgumentException>()
            .Where(e => e.ParamName != null && e.ParamName.Contains(parametroEsperado, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Calcular_creditos_destino_cero_lanza_invalidoperation_con_mensaje_denominador()
    {
        var p = new ParametrosCreditos(3, 0, 2, 1, false, "CAU-P-011");
        var act = () => _strategy.Calcular(p);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*denominador nulo*");
    }

    [Fact]
    public void Calcular_ruta_normativa_vacia_lanza_argumentexception()
    {
        var p = new ParametrosCreditos(3, 3, 2, 1, false, "   ");
        var act = () => _strategy.Calcular(p);

        act.Should().Throw<ArgumentException>()
            .Where(e => e.ParamName != null && e.ParamName.Contains("RutaNormativa", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Calcular_detalle_incluye_trazabilidad_completa()
    {
        var p = new ParametrosCreditos(4, 3, 5, 1, false, "CAU-P-011");
        var r = _strategy.Calcular(p);

        r.DetalleCalculo.Should().Contain("Pcred");
        r.DetalleCalculo.Should().Contain("4");
        r.DetalleCalculo.Should().Contain("3");
        r.DetalleCalculo.Should().Contain("CAU-P-011");
    }
}