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

        sinSegundo.Errores.Should().Contain(e => e.StartsWith("Falta el docente revisor 2"));
        repetido.Errores.Should().Contain(e => e.Contains("personas distintas"));
    }

    // ---------- clasificación de problemas: no es el certificado, campos sin llenar, reglas ----------

    [Fact]
    public void Un_plan_analitico_se_clasifica_como_no_es_el_certificado()
    {
        var resultado = LectorCertificadoSiha.Leer(Pdf("plan-analitico-no-valido.pdf"));

        resultado.Problemas.Should().ContainSingle()
            .Which.Should().Match<ProblemaCertificado>(p => p.Tipo == TiposProblema.NoEsCertificado && p.Seccion == SeccionesCertificado.Documento);
    }

    [Fact]
    public void Un_archivo_que_no_es_pdf_se_clasifica_como_no_es_el_certificado()
    {
        LectorCertificadoSiha.Leer("hola"u8.ToArray()).Problemas.Single().Tipo.Should().Be(TiposProblema.NoEsCertificado);
    }

    [Fact]
    public void Etiqueta_sin_valor_se_reporta_como_campo_sin_llenar_en_su_seccion()
    {
        var lineas = Base().Select(l => l == "Cédula: 1804567890" ? "Cédula:" : l).ToList();

        var problema = LectorCertificadoSiha.LeerTexto(lineas).Problemas.Should().ContainSingle().Subject;
        problema.Tipo.Should().Be(TiposProblema.CampoVacio);
        problema.Seccion.Should().Be(SeccionesCertificado.Estudiante);
        problema.Mensaje.Should().Be("Falta la cédula del estudiante: 'Cédula:' está en el certificado pero sin llenar.");
    }

    [Fact]
    public void Etiqueta_que_no_aparece_se_reporta_como_campo_sin_llenar()
    {
        var problema = LectorCertificadoSiha.LeerTexto(Base().Where(l => l != "Cédula: 1804567890")).Problemas.Single();

        problema.Tipo.Should().Be(TiposProblema.CampoVacio);
        problema.Mensaje.Should().StartWith("Falta la cédula del estudiante");
    }

    [Fact]
    public void Periodo_academico_vacio_de_una_materia_dice_de_cual_materia_es()
    {
        var lineas = Base().Select(l => l.Replace("Nivel: 2 Período académico: Oct 2025-Mar 2026 Nota: 8 Estado: Aprobada",
            "Nivel: 2 Período académico: Nota: 8 Estado: Aprobada")).ToList();

        var problema = LectorCertificadoSiha.LeerTexto(lineas).Problemas.Single();
        problema.Tipo.Should().Be(TiposProblema.CampoVacio);
        problema.Seccion.Should().Be(SeccionesCertificado.Historial);
        problema.Mensaje.Should().Contain("Falta el período académico de «Física II»");
    }

    [Theory]
    [InlineData("Estudiante: Ana", "Estudiante: Apellidos y nombres del estudiante", SeccionesCertificado.Estudiante)]
    [InlineData("Cédula: 1804567890", "Cédula: __________", SeccionesCertificado.Estudiante)]
    [InlineData("Carrera de destino: Software", "Carrera de destino: [Nombre completo de la carrera]", SeccionesCertificado.Tramite)]
    [InlineData("Docente revisor 1: Ing. Ana Ruiz - aruiz@uta.edu.ec", "Docente revisor 1: Nombre - correo@uta.edu.ec", SeccionesCertificado.Revisores)]
    public void Texto_de_ejemplo_de_la_plantilla_sin_reemplazar_se_detecta(string original, string ejemplo, string seccion)
    {
        var lineas = Base().Select(l => l == original ? ejemplo : l).ToList();

        var problema = LectorCertificadoSiha.LeerTexto(lineas).Problemas.Should().ContainSingle().Subject;
        problema.Tipo.Should().Be(TiposProblema.CampoVacio);
        problema.Seccion.Should().Be(seccion);
        problema.Mensaje.Should().Contain("texto de ejemplo de la plantilla");
    }

    [Fact]
    public void Datos_incoherentes_se_clasifican_como_reglas()
    {
        var lineas = Base().Select(l => l.Replace("Nota: 5.5 Estado: Reprobada", "Nota: 5.5 Estado: Aprobada")
            .Replace("Carrera de origen: Ingeniería Industrial", "Carrera de origen: IND")).ToList();

        var problemas = LectorCertificadoSiha.LeerTexto(lineas).Problemas;
        problemas.Should().OnlyContain(p => p.Tipo == TiposProblema.Regla);
        problemas.Should().Contain(p => p.Seccion == SeccionesCertificado.Historial && p.Mensaje.Contains("Química"));
        problemas.Should().Contain(p => p.Seccion == SeccionesCertificado.Tramite && p.Mensaje.Contains("sin siglas"));
    }

    private static byte[] PdfDePrueba(string nombre)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!Directory.Exists(Path.Combine(dir!.FullName, "documentos-prueba"))) dir = dir.Parent;
        return File.ReadAllBytes(Path.Combine(dir.FullName, "documentos-prueba", "certificados-pruebas", nombre));
    }

    [Theory]
    [InlineData("E01-cedula-9-digitos.pdf", TiposProblema.Regla)]
    [InlineData("E08-nota-incoherente.pdf", TiposProblema.Regla)]
    [InlineData("E09-carrera-con-siglas.pdf", TiposProblema.Regla)]
    [InlineData("E18-pdf-escaneado.pdf", TiposProblema.NoEsCertificado)]
    [InlineData("E19-plan-analitico-suelto.pdf", TiposProblema.NoEsCertificado)]
    [InlineData("E20-campos-sin-llenar.pdf", TiposProblema.CampoVacio)]
    [InlineData("E21-texto-de-ejemplo.pdf", TiposProblema.CampoVacio)]
    public void Cada_pdf_de_prueba_se_rechaza_con_el_tipo_de_problema_correcto(string archivo, string tipo)
    {
        var resultado = LectorCertificadoSiha.Leer(PdfDePrueba(archivo));

        resultado.EsValido.Should().BeFalse();
        resultado.Problemas.Should().NotBeEmpty().And.OnlyContain(p => p.Tipo == tipo);
    }

    [Fact]
    public void El_pdf_con_campos_sin_llenar_dice_cuales_faltan()
    {
        var mensajes = LectorCertificadoSiha.Leer(PdfDePrueba("E20-campos-sin-llenar.pdf")).Errores;

        mensajes.Should().Contain(m => m.StartsWith("Falta la cédula del estudiante"));
        mensajes.Should().Contain(m => m.Contains("Falta el período académico de «Física»"));
    }

    [Fact]
    public void Todos_los_pdf_que_deben_rechazarse_se_rechazan_y_los_validos_se_leen()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!Directory.Exists(Path.Combine(dir!.FullName, "documentos-prueba"))) dir = dir.Parent;
        var carpeta = Path.Combine(dir.FullName, "documentos-prueba", "certificados-pruebas");

        foreach (var ruta in Directory.GetFiles(carpeta, "*.pdf"))
        {
            var nombre = Path.GetFileName(ruta);
            var resultado = LectorCertificadoSiha.Leer(File.ReadAllBytes(ruta));
            // E04 (revisor que no existe) y E16 (carrera fuera del catálogo) se rechazan al registrar, no al leer.
            var seRechazaAlLeer = nombre.StartsWith('E') && nombre is not ("E04-revisor-no-existe.pdf" or "E16-carrera-no-existe.pdf");
            resultado.EsValido.Should().Be(!seRechazaAlLeer, $"{nombre}: {string.Join(" | ", resultado.Errores)}");
        }
    }

    [Theory]
    [InlineData("Ana Pérez", false)]
    [InlineData("1804567890", false)]
    [InlineData("Física I", false)]
    [InlineData("____", true)]
    [InlineData("XXXXXXXXXX", true)]
    [InlineData("<nombre>", true)]
    [InlineData("AAAA-MM-DD", true)]
    [InlineData("Escriba aquí", true)]
    public void Reconoce_el_texto_de_ejemplo(string valor, bool esEjemplo)
        => LectorCertificadoSiha.EsTextoDeEjemplo(valor).Should().Be(esEjemplo);

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
