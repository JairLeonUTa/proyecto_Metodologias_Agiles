using FluentAssertions;
using Siha.MsA.Api.Services;

namespace Siha.MsA.Tests;

public class ParametrosValidatorTests
{
    [Theory]
    [InlineData(80d, 10d, 70d)]
    [InlineData(0d, 0d, 0d)]
    [InlineData(100d, 100d, 100d)]
    public void Valida_parametros_dentro_de_rango(double theta, double delta, double tau)
    {
        var (valido, error) = ParametrosValidator.Validar(theta, delta, tau);

        valido.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void Rechaza_banda_mayor_que_umbral()
    {
        var (valido, error) = ParametrosValidator.Validar(60d, 75d, 50d);

        valido.Should().BeFalse();
        error.Should().Contain("δ");
    }

    [Theory]
    [InlineData(-1d, 0d, 0d)]
    [InlineData(101d, 0d, 0d)]
    [InlineData(50d, 50d, 120d)]
    public void Rechaza_valores_fuera_de_rango(double theta, double delta, double tau)
    {
        var (valido, _) = ParametrosValidator.Validar(theta, delta, tau);

        valido.Should().BeFalse();
    }
}
