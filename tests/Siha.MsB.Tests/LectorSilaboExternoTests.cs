using FluentAssertions;
using Siha.Certificados;

namespace Siha.MsB.Tests;

/// <summary>
/// Lector del sílabo (plan analítico) de otra universidad para la movilidad externa: debe encontrar
/// las unidades con sus temas y horas en formatos razonables, y rechazar con mensajes claros los
/// documentos con los que no se puede comparar nada.
/// </summary>
public class LectorSilaboExternoTests
{
    private static ResultadoLecturaSilabo Leer(params string[] lineas)
        => LectorSilaboExterno.LeerTexto(lineas, "Física I");

    [Fact]
    public void Lee_unidades_con_temas_y_horas()
    {
        var r = Leer(
            "ESCUELA SUPERIOR POLITÉCNICA DE CHIMBORAZO",
            "SÍLABO DE LA ASIGNATURA",
            "Asignatura: Física I",
            "Créditos: 3",
            "Unidad 1: Estática (24 horas)",
            "Fuerzas y equilibrio",
            "Diagramas de cuerpo libre",
            "Unidad 2: Dinámica (24 horas)",
            "Leyes de Newton",
            "Trabajo y energía",
            "Bibliografía",
            "Serway, Física.");

        r.EsValido.Should().BeTrue(string.Join(" | ", r.Errores));
        var s = r.Silabo!;
        s.Materia.Should().Be("Física I");
        s.NombreEnSilabo.Should().Be("Física I");
        s.Creditos.Should().Be(3);
        s.Unidades.Should().HaveCount(2);
        s.HorasTotales.Should().Be(48);
        s.Unidades[0].Nombre.Should().Be("Estática");
        s.Unidades[0].Horas.Should().Be(24);
        s.Unidades[0].Contenidos.Should().Contain("Fuerzas y equilibrio").And.Contain("Diagramas de cuerpo libre");
        // La bibliografía no se mezcla con los temas de la última unidad.
        s.Unidades[1].Contenidos.Should().NotContain("Serway");
    }

    [Fact]
    public void Acepta_numeros_romanos_y_reparte_las_horas_totales()
    {
        var r = Leer(
            "Materia: Física I",
            "Carga horaria: 90",
            "UNIDAD I. Estática",
            "Equilibrio de partículas",
            "UNIDAD II. Dinámica",
            "Leyes del movimiento",
            "UNIDAD III. Energía",
            "Trabajo y potencia");

        r.EsValido.Should().BeTrue(string.Join(" | ", r.Errores));
        r.Silabo!.Unidades.Should().HaveCount(3);
        r.Silabo.Unidades.Should().OnlyContain(u => u.Horas == 30);
        r.Silabo.HorasTotales.Should().Be(90);
    }

    [Fact]
    public void Sin_unidades_se_rechaza_con_mensaje_claro()
    {
        var r = Leer("Asignatura: Física I", "Créditos: 3", "Contenido general del curso.");

        r.EsValido.Should().BeFalse();
        r.Errores.Should().ContainSingle(e => e.Contains("no se encontraron unidades") && e.Contains("Física I"));
    }

    [Fact]
    public void Sin_horas_por_unidad_ni_totales_se_rechaza()
    {
        var r = Leer(
            "Asignatura: Física I",
            "Unidad 1: Estática",
            "Fuerzas",
            "Unidad 2: Dinámica",
            "Movimiento");

        r.EsValido.Should().BeFalse();
        r.Errores.Should().ContainSingle(e => e.Contains("no indica las horas"));
    }

    [Fact]
    public void Una_unidad_sin_temas_se_rechaza()
    {
        var r = Leer(
            "Asignatura: Física I",
            "Unidad 1: Estática (24 horas)",
            "Unidad 2: Dinámica (24 horas)",
            "Leyes de Newton");

        r.EsValido.Should().BeFalse();
        r.Errores.Should().ContainSingle(e => e.Contains("unidad 1", StringComparison.OrdinalIgnoreCase) && e.Contains("no tiene temas"));
    }

    [Fact]
    public void Un_certificado_siha_no_sirve_como_silabo()
    {
        var r = Leer(
            "CERTIFICADO DE HOMOLOGACIÓN",
            "Formato SIHA-CH-01",
            "Estudiante: Pérez Ana");

        r.EsValido.Should().BeFalse();
        r.Errores.Should().ContainSingle(e => e.Contains("certificado de homologación"));
    }

    [Fact]
    public void Un_archivo_que_no_es_pdf_se_rechaza()
    {
        var r = LectorSilaboExterno.Leer(new byte[] { 1, 2, 3 }, "Física I");

        r.EsValido.Should().BeFalse();
        r.Errores.Should().ContainSingle(e => e.Contains("no es un archivo PDF"));
    }
}
