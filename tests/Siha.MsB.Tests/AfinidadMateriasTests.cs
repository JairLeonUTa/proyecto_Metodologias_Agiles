using FluentAssertions;
using Siha.Certificados;
using Siha.MsB.Tests.Soporte;

namespace Siha.MsB.Tests;

/// <summary>Comprobación de que lo que se pide homologar se relaciona, con las mallas reales de la UTA.</summary>
public class AfinidadMateriasTests
{
    private static AfinidadMaterias Entre(string origen, string destino)
        => new(CatalogoReal.Malla(origen), CatalogoReal.Malla(destino));

    [Theory]
    [InlineData("IND", "ALGEBRA", "SW", "ALGEBRA LINEAL")]
    [InlineData("IND", "PROGRAMACION", "SW", "ALGORITMOS Y LOGICA DE PROGRAMACION")]
    [InlineData("TEL", "CALCULO DE UNA VARIABLE", "TI", "CALCULO DIFERENCIAL")]
    [InlineData("TEL", "BASE DE DATOS", "TI", "FUNDAMENTOS DE BASES DE DATOS")]
    [InlineData("IND", "ESTADISTICA Y PROBABILIDADES", "SW", "PROBABILIDAD Y ESTADISTICA")]
    [InlineData("ROB", "MECANICA BASICA", "SW", "FISICA")]
    [InlineData("ROB", "PROGRAMACION AVANZADA", "SW", "PROGRAMACION ORIENTADA A OBJETOS")]
    public void Materias_que_tratan_lo_mismo_se_relacionan(string co, string origen, string cd, string destino)
        => Entre(co, cd).Calcular(new[] { origen }, destino).SeRelacionan.Should().BeTrue();

    [Theory]
    [InlineData("IND", "QUIMICA", "SW", "ALGORITMOS Y LOGICA DE PROGRAMACION")]
    [InlineData("IND", "REALIDAD NACIONAL", "SW", "CALCULO DIFERENCIAL")]
    [InlineData("IND", "ALGEBRA", "SW", "SISTEMAS OPERATIVOS")]
    [InlineData("TEL", "QUIMICA", "TI", "ESTRUCTURA DE DATOS")]
    [InlineData("ROB", "GESTION AMBIENTAL", "SW", "FISICA")]
    public void Cualquier_materia_por_cualquier_otra_no_se_relaciona(string co, string origen, string cd, string destino)
        => Entre(co, cd).Calcular(new[] { origen }, destino).SeRelacionan.Should().BeFalse();

    [Fact]
    public void Movilidad_externa_compara_por_el_nombre_contra_los_temas_de_destino()
    {
        var afinidad = new AfinidadMaterias(Array.Empty<MateriaCatalogo>(), CatalogoReal.Malla("SW"));

        afinidad.Calcular(new[] { "Cálculo I" }, "Cálculo Diferencial").SeRelacionan.Should().BeTrue();
        afinidad.Calcular(new[] { "Contabilidad General" }, "Física").SeRelacionan.Should().BeFalse();
    }

    [Fact]
    public void Si_la_materia_de_destino_no_esta_en_la_malla_no_se_bloquea()
        => Entre("IND", "SW").Calcular(new[] { "QUIMICA" }, "Materia Inventada").SeRelacionan.Should().BeTrue();

    [Fact]
    public void Sugiere_una_materia_de_destino_distinta_para_cada_aprobada()
    {
        var sugerencias = Entre("IND", "SW").Sugerir(new[] { "Física Básica", "Álgebra", "Programación", "Química", "Cálculo Diferencial" });

        sugerencias.Select(s => (s.MateriaOrigen, s.MateriaDestino)).Should().Contain(new[]
        {
            ("Física Básica", "Fisica"), ("Álgebra", "Algebra Lineal"),
            ("Programación", "Algoritmos y Logica de Programacion"), ("Cálculo Diferencial", "Calculo Diferencial")
        });
        sugerencias.Select(s => s.MateriaDestino).Should().OnlyHaveUniqueItems();
        sugerencias.Should().NotContain(s => s.MateriaOrigen == "Química", "no hay una materia de Software que trate lo mismo");
    }
}
