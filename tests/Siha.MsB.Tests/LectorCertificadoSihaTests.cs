using FluentAssertions;
using Siha.Certificados;
using Siha.Contracts;
using Xunit;

namespace Siha.MsB.Tests;

/// <summary>
/// Lectura de la plantilla oficial SIHA-CH-01: un único PDF preaprobado trae todos los datos de la
/// solicitud. Se prueba con los PDF reales de <c>documentos-prueba/certificados</c>.
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
    public void Certificado_industrial_a_software_se_lee_completo()
    {
        var resultado = LectorCertificadoSiha.Leer(Pdf("SIHA-IND-SW-completo.pdf"));

        resultado.Errores.Should().BeEmpty();
        resultado.EsValido.Should().BeTrue();
        var c = resultado.Certificado!;
        c.NumeroCertificado.Should().Be("SIHA-2026-0147");
        c.FechaEmision.Should().Be(new DateOnly(2026, 10, 30));
        c.EstudianteNombre.Should().Be("Pérez Gómez Ana Lucía");
        c.EstudianteIdentificacion.Should().Be("1804567890");
        c.TipoTramite.Should().Be(TiposTramite.Interno);
        c.RutaNormativa.Should().Be(RutasNormativas.CambioCarreraOu);
        c.InstitucionOrigen.Should().Be("Universidad Técnica de Ambato");
        c.CarreraOrigenCodigo.Should().Be("IND");
        c.CarreraDestinoCodigo.Should().Be("SW");
        c.CarreraDestinoNombre.Should().Be("Ingeniería de Software");

        c.Asignaturas.Select(a => a.Codigo).Should().Equal("IND105", "IND108", "IND205");
        var fisica1 = c.Asignaturas[0];
        fisica1.Nombre.Should().Be("Física I");
        fisica1.Semestre.Should().Be(1);
        fisica1.Creditos.Should().Be(4);
        fisica1.HorasTotales.Should().Be(192);
        fisica1.FechaAprobacion.Should().Be(new DateOnly(2024, 2, 16));
        fisica1.Nota.Should().Be(8.60m);
        c.Revisores.Should().Equal(
            new RevisorCertificado("Ing. Hernán Naranjo", "hnaranjo@uta.edu.ec"),
            new RevisorCertificado("Ing. María Vaca", "mvaca@uta.edu.ec"));
        fisica1.Unidades.Should().HaveCount(4);
        fisica1.Unidades[1].Nombre.Should().Be("Cinemática de la partícula");
        fisica1.Unidades[1].Horas.Should().Be(48);
        // El texto partido en dos líneas del PDF se vuelve a unir.
        fisica1.Unidades[0].Contenidos.Should().EndWith("producto escalar y producto vectorial.");
        fisica1.Unidades[0].Contenidos.Should().Contain("vectores en el plano y en el espacio");
    }

    [Fact]
    public void Contenidos_que_cruzan_de_pagina_no_arrastran_el_pie_de_pagina()
    {
        var c = LectorCertificadoSiha.Leer(Pdf("SIHA-IND-SW-completo.pdf")).Certificado!;

        c.Asignaturas.SelectMany(a => a.Unidades).Should()
            .NotContain(u => u.Contenidos.Contains("Página") || u.Contenidos.Contains("SIHA-CH-01"));
        c.Asignaturas.Single(a => a.Codigo == "IND205").Unidades.Should().HaveCount(4);
    }

    [Fact]
    public void Certificado_externo_lee_institucion_y_ruta_de_movilidad()
    {
        var r = LectorCertificadoSiha.Leer(Pdf("SIHA-UCuenca-SW-externo.pdf"));
        r.Errores.Should().BeEmpty();
        var c = r.Certificado!;

        c.TipoTramite.Should().Be(TiposTramite.Externo);
        c.RutaNormativa.Should().Be(RutasNormativas.Movilidad);
        c.InstitucionOrigen.Should().Be("Universidad de Cuenca");
        c.Asignaturas.Single(a => a.Codigo == "UC-PROG").FechaAprobacion.Should().Be(new DateOnly(2021, 7, 23));
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
        resultado.Errores.Should().Contain(e => e.Contains("cédula") && e.Contains("10 dígitos"));
        resultado.Errores.Should().Contain(e => e.Contains("no es aprobatoria"));
        resultado.Errores.Should().Contain(e => e.Contains("no coinciden con las horas totales"));
        resultado.Errores.Should().Contain(e => e.Contains("hace más de 10 años"));
        resultado.Errores.Should().Contain(e => e.Contains("docente@gmail.com") && e.Contains("institucional"));
    }

    [Fact]
    public void Archivo_que_no_es_pdf_se_rechaza()
    {
        LectorCertificadoSiha.Leer("hola"u8.ToArray()).Errores.Should().ContainSingle("El archivo no es un PDF.");
    }

    private static IEnumerable<string> Base(string tipo = "INTERNO", string ruta = "CAU-P-011") => new[]
    {
        "CERTIFICADO DE HOMOLOGACIÓN SIHA-IA", "Formato SIHA-CH-01",
        "Número de certificado: SIHA-1 Fecha de emisión: 2026-10-30", "Emitido por: Secretaría",
        "Estudiante: Ana", "Cédula: 1804567890",
        $"Tipo de trámite: {tipo} Ruta normativa: {ruta}", "Institución de origen: UTA",
        "Carrera de origen: IND - Industrial", "Carrera de destino: SW - Software",
        "Docente revisor 1: Ing. Ana Ruiz - aruiz@uta.edu.ec", "Docente revisor 2: Ing. Luis Paz <lpaz@uta.edu.ec>",
        "ASIGNATURAS APROBADAS",
        "Asignatura: IND105 - Física I", "Semestre: 1 Créditos: 4 Horas totales: 10",
        "Fecha de aprobación: 15/02/2024 Nota: 8,5",
        "Unidad 1: Vectores (10 horas)", "Contenidos: Suma de vectores; nota: incluye producto escalar.",
        "FIN DEL CERTIFICADO", "Estudiante: esto ya no se lee"
    };

    [Fact]
    public void Texto_admite_fechas_ddmmaaaa_comas_decimales_y_etiquetas_dentro_de_contenidos()
    {
        var resultado = LectorCertificadoSiha.LeerTexto(Base());

        resultado.Errores.Should().BeEmpty();
        var a = resultado.Certificado!.Asignaturas.Single();
        a.FechaAprobacion.Should().Be(new DateOnly(2024, 2, 15));
        a.Nota.Should().Be(8.5m);
        a.Unidades.Single().Contenidos.Should().Be("Suma de vectores; nota: incluye producto escalar.");
        resultado.Certificado.EstudianteNombre.Should().Be("Ana");
    }

    [Fact]
    public void Lee_los_dos_revisores_con_guion_o_con_angulos()
    {
        var revisores = LectorCertificadoSiha.LeerTexto(Base()).Certificado!.Revisores;

        revisores.Should().Equal(
            new RevisorCertificado("Ing. Ana Ruiz", "aruiz@uta.edu.ec"),
            new RevisorCertificado("Ing. Luis Paz", "lpaz@uta.edu.ec"));
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
    public void Tramite_interno_con_ruta_de_movilidad_se_rechaza()
    {
        var resultado = LectorCertificadoSiha.LeerTexto(Base("INTERNO", "CAU-P-122"));

        resultado.EsValido.Should().BeFalse();
        resultado.Errores.Should().Contain(e => e.Contains("INTERNO") && e.Contains("CAU-P-011"));
    }

    [Fact]
    public void Faltan_datos_obligatorios_se_informan_uno_por_uno()
    {
        var lineas = Base().Where(l => !l.StartsWith("Cédula") && !l.StartsWith("Fecha de aprobación")).ToList();

        var resultado = LectorCertificadoSiha.LeerTexto(lineas);

        resultado.Errores.Should().Contain("Falta el dato 'Cédula'.");
        resultado.Errores.Should().Contain(e => e.Contains("IND105") && e.Contains("Fecha de aprobación"));
        resultado.Errores.Should().Contain(e => e.Contains("IND105") && e.Contains("Nota"));
    }
}
