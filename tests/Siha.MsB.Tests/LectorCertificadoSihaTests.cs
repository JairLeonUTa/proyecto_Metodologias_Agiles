using FluentAssertions;
using Siha.Certificados;
using Siha.Contracts;
using Xunit;

namespace Siha.MsB.Tests;

/// <summary>
/// Lectura de la plantilla oficial SIHA-CH-01: estudiante, carreras con nombre completo, historial
/// (aprobadas y reprobadas con período académico y nota), materias a homologar y dos revisores.
/// Se prueba con los PDF reales de <c>documentos-prueba/certificados</c>.
/// </summary>
public class LectorCertificadoSihaTests
{
    private static byte[] Pdf(string nombre)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "documentos-prueba")))
            dir = dir.Parent;
        dir.Should().NotBeNull("los PDF de ejemplo viven en documentos-prueba/certificados");
        return File.ReadAllBytes(Path.Combine(dir!.FullName, "documentos-prueba", "certificados", nombre));
    }

    [Fact]
    public void Certificado_TI_a_Software_se_lee_completo()
    {
        var resultado = LectorCertificadoSiha.Leer(Pdf("SIHA-TI-SW-cambio-de-carrera.pdf"));

        resultado.Errores.Should().BeEmpty();
        var c = resultado.Certificado!;
        c.NumeroCertificado.Should().Be("SIHA-2026-0147");
        c.EstudianteNombre.Should().Be("Pérez Gómez Ana Lucía");
        c.TipoTramite.Should().Be(TiposTramite.Interno);
        c.RutaNormativa.Should().Be(RutasNormativas.CambioCarreraOu);
        c.CarreraOrigen.Should().Be("Tecnologías de la Información");
        c.CarreraDestino.Should().Be("Software");

        c.Materias.Should().HaveCount(11);
        c.Materias.Count(m => !m.Aprobada).Should().Be(2, "perdió Cálculo Integral y Medidas Eléctricas");
        var fundamentos = c.Materias.Single(m => m.Nombre == "Fundamentos de Programación");
        fundamentos.Nivel.Should().Be(1);
        fundamentos.PeriodoAcademico.Should().Be("Abr-Sep 2025");
        fundamentos.Nota.Should().Be(8.90m);
        c.Materias.Single(m => m.Nombre == "Medidas Eléctricas").Aprobada.Should().BeFalse();

        c.Solicitudes.Should().HaveCount(5);
        c.Solicitudes[0].MateriasOrigen.Should().Equal("Fundamentos de Programación");
        c.Solicitudes[0].MateriaDestino.Should().Be("Algoritmos y Lógica de Programación");
        c.Revisores.Select(r => r.Correo).Should().Equal("hnaranjo@uta.edu.ec", "mvaca@uta.edu.ec");
    }

    [Fact]
    public void Plan_analitico_suelto_se_rechaza_por_no_ser_la_plantilla()
    {
        var resultado = LectorCertificadoSiha.Leer(Pdf("plan-analitico-no-valido.pdf"));

        resultado.EsValido.Should().BeFalse();
        resultado.Errores.Should().ContainSingle().Which.Should().Contain("SIHA-CH-01");
    }

    [Fact]
    public void Certificado_con_errores_lista_todos_los_problemas()
    {
        var resultado = LectorCertificadoSiha.Leer(Pdf("SIHA-con-errores.pdf"));

        resultado.EsValido.Should().BeFalse();
        resultado.Errores.Should().Contain(e => e.Contains("cédula"));
        resultado.Errores.Should().Contain(e => e.Contains("nombre completo de la carrera"));
        resultado.Errores.Should().Contain(e => e.Contains("primer período académico"));
        resultado.Errores.Should().Contain(e => e.Contains("'Física' está reprobada"));
        resultado.Errores.Should().Contain(e => e.Contains("hace más de 10 años"));
        resultado.Errores.Should().Contain(e => e.Contains("docente@gmail.com"));
    }

    [Fact]
    public void Archivo_que_no_es_pdf_se_rechaza()
    {
        LectorCertificadoSiha.Leer("hola"u8.ToArray()).Errores.Should().ContainSingle("El archivo no es un PDF.");
    }

    private static List<string> Base() => new()
    {
        "CERTIFICADO DE HOMOLOGACIÓN SIHA-IA", "Formato SIHA-CH-01",
        "Número de certificado: SIHA-1 Fecha de emisión: 2026-10-30", "Emitido por: Secretaría",
        "Estudiante: Ana", "Cédula: 1804567890",
        "Tipo de trámite: Cambio de carrera Ruta normativa: CAU-P-011", "Institución de origen: UTA",
        "Carrera de origen: Ingeniería Industrial", "Carrera de destino: Software",
        "Docente revisor 1: Ing. Ana Ruiz - aruiz@uta.edu.ec", "Docente revisor 2: Ing. Luis Paz <lpaz@uta.edu.ec>",
        "Materia: Física I", "Nivel: 1 Período académico: Abr-Sep 2025 Nota: 8,5 Estado: Aprobada",
        "Materia: Física II", "Nivel: 2 Período académico: Oct 2025-Mar 2026 Nota: 8 Estado: Aprobada",
        "Materia: Química", "Nivel: 2 Período académico: Oct 2025-Mar 2026 Nota: 5.5 Estado: Reprobada",
        "Solicitud 1", "Materia aprobada: Física I", "Materia aprobada: Física II", "Homologar por: Física",
        "FIN DEL CERTIFICADO", "Estudiante: esto ya no se lee"
    };

    [Fact]
    public void Varias_materias_aprobadas_pueden_pedirse_para_una_sola_de_destino()
    {
        var resultado = LectorCertificadoSiha.LeerTexto(Base());

        resultado.Errores.Should().BeEmpty();
        var s = resultado.Certificado!.Solicitudes.Single();
        s.MateriasOrigen.Should().Equal("Física I", "Física II");
        s.MateriaDestino.Should().Be("Física");
        resultado.Certificado.Materias.Single(m => m.Nombre == "Química").Aprobada.Should().BeFalse();
        resultado.Certificado.EstudianteNombre.Should().Be("Ana");
    }

    [Fact]
    public void Reprobar_en_segundo_nivel_no_impide_la_solicitud_pero_no_se_puede_pedir_una_reprobada()
    {
        var conReprobada = Base();
        conReprobada.InsertRange(conReprobada.IndexOf("FIN DEL CERTIFICADO"), new[] { "Materia aprobada: Química", "Homologar por: Química" });

        LectorCertificadoSiha.LeerTexto(Base()).EsValido.Should().BeTrue("una reprobada en el segundo nivel no impide el cambio");
        LectorCertificadoSiha.LeerTexto(conReprobada).Errores.Should().Contain(e => e.Contains("'Química' está reprobada"));
    }

    [Fact]
    public void Estado_y_nota_deben_coincidir()
    {
        var lineas = Base().Select(l => l.Replace("Nota: 5.5 Estado: Reprobada", "Nota: 5.5 Estado: Aprobada")).ToList();

        LectorCertificadoSiha.LeerTexto(lineas).Errores.Should().Contain(e => e.Contains("Química") && e.Contains("menor que 7"));
    }

    [Fact]
    public void Las_notas_del_rechazo_se_muestran_con_coma_y_dos_decimales()
    {
        var lineas = Base().Select(l => l.Replace("Nota: 5.5 Estado: Reprobada", "Nota: 6.5 Estado: Aprobada")).ToList();

        LectorCertificadoSiha.LeerTexto(lineas).Errores.Should()
            .Contain(e => e.Contains("su nota 6,50 es menor que 7,00"))
            .And.NotContain(e => e.Contains("6.5"));
    }

    [Fact]
    public void Sin_dos_revisores_distintos_se_rechaza()
    {
        var sinSegundo = LectorCertificadoSiha.LeerTexto(Base().Where(l => !l.StartsWith("Docente revisor 2")));
        var repetido = LectorCertificadoSiha.LeerTexto(Base().Select(l =>
            l.StartsWith("Docente revisor 2") ? "Docente revisor 2: Otra - aruiz@uta.edu.ec" : l));

        sinSegundo.Errores.Should().Contain("Falta el dato 'Docente revisor 2'.");
        repetido.Errores.Should().Contain(e => e.Contains("personas distintas"));
    }

    [Fact]
    public void Una_materia_aprobada_no_se_usa_en_dos_solicitudes()
    {
        var lineas = Base();
        lineas.InsertRange(lineas.IndexOf("FIN DEL CERTIFICADO"), new[] { "Materia aprobada: Física I", "Homologar por: Física General" });

        LectorCertificadoSiha.LeerTexto(lineas).Errores.Should().Contain(e => e.Contains("una sola homologación"));
    }

    [Fact]
    public void Movilidad_con_ruta_de_cambio_de_carrera_se_rechaza()
    {
        var lineas = Base().Select(l => l.Replace("Cambio de carrera", "Movilidad externa")).ToList();

        LectorCertificadoSiha.LeerTexto(lineas).Errores.Should().Contain(e => e.Contains("CAU-P-122"));
    }
}
