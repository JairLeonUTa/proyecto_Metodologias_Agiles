using FluentAssertions;
using Siha.MsA.Api.Services;

namespace Siha.MsA.Tests;

public class ParametrosValidatorTests
{
    [Theory]
    [InlineData(80d, 10d, 70d)]
    [InlineData(80d, 0d, 0d)]
    [InlineData(100d, 100d, 100d)]
    [InlineData(90d, 15d, 75d)]
    public void Valida_parametros_dentro_de_rango(double theta, double delta, double tau)
    {
        var (valido, error) = ParametrosValidator.Validar(theta, delta, tau);

        valido.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void Rechaza_banda_mayor_que_umbral()
    {
        var (valido, error) = ParametrosValidator.Validar(85d, 90d, 50d);

        valido.Should().BeFalse();
        error.Should().Contain("δ");
    }

    [Theory]
    [InlineData(-1d, 0d, 0d)]
    [InlineData(101d, 0d, 0d)]
    [InlineData(80d, 50d, 120d)]
    public void Rechaza_valores_fuera_de_rango(double theta, double delta, double tau)
    {
        var (valido, _) = ParametrosValidator.Validar(theta, delta, tau);

        valido.Should().BeFalse();
    }

    [Theory]
    [InlineData(79.99d)]
    [InlineData(60d)]
    [InlineData(0d)]
    public void No_permite_bajar_el_contenido_minimo_del_80_del_reglamento(double theta)
    {
        var (valido, error) = ParametrosValidator.Validar(theta, 0d, 75d);

        valido.Should().BeFalse();
        error.Should().Contain("80 %").And.Contain("reglamento");
    }

    [Fact]
    public void El_minimo_del_reglamento_es_80()
        => ParametrosValidator.ThetaMinimoReglamento.Should().Be(80d);
}
