using FluentAssertions;
using Siha.Certificados;

namespace Siha.MsB.Tests;

/// <summary>Comparación de nombres de materias y sugerencia del nombre oficial más parecido.</summary>
public class NombresMateriasTests
{
    private static readonly string[] MallaSw =
    {
        "FISICA", "ALGEBRA LINEAL", "ALGORITMOS Y LOGICA DE PROGRAMACION", "CALCULO DIFERENCIAL",
        "PROGRAMACION ORIENTADA A OBJETOS", "ESTRUCTURA DE DATOS"
    };

    [Theory]
    [InlineData("Física")]
    [InlineData("Álgebra Lineal")]
    [InlineData("Algoritmos y Lógica de Programación")]
    [InlineData("programación orientada a objetos")]
    public void El_nombre_oficial_con_tildes_o_minusculas_existe(string nombre)
        => NombresMaterias.Existe(nombre, MallaSw).Should().BeTrue();

    [Theory]
    [InlineData("Algoritmos y Logica", "ALGORITMOS Y LOGICA DE PROGRAMACION")]
    [InlineData("Estructuras de Datos", "ESTRUCTURA DE DATOS")]
    [InlineData("Calculo Diferencal", "CALCULO DIFERENCIAL")]
    [InlineData("Programación Orientada a Objeto", "PROGRAMACION ORIENTADA A OBJETOS")]
    public void Sugiere_el_nombre_oficial_mas_parecido(string escrito, string esperado)
    {
        NombresMaterias.Existe(escrito, MallaSw).Should().BeFalse();
        NombresMaterias.MasParecida(escrito, MallaSw).Should().Be(esperado);
    }

    [Fact]
    public void No_sugiere_nada_si_ninguna_se_parece()
        => NombresMaterias.MasParecida("Mecánica Cuántica Avanzada", MallaSw).Should().BeNull();

    [Theory]
    [InlineData("ALGORITMOS Y LOGICA DE PROGRAMACION", "Algoritmos y Logica de Programacion")]
    [InlineData("FISICA II", "Fisica II")]
    [InlineData("Física", "Física")]
    public void Muestra_el_nombre_en_formato_titulo(string nombre, string esperado)
        => NombresMaterias.Titulo(nombre).Should().Be(esperado);
}
