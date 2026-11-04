using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Siha.MsC.Tests.Soporte;
using Siha.MsC.Api.Domain;
using Siha.MsC.Api.Options;
using Siha.MsC.Api.Services;

namespace Siha.MsC.Tests;

/// <summary>
/// Pruebas unitarias para el extractor de PDF y detector de escaneados (US-07, S2-P01, S2-P02).
/// Responsable: Paul Acurio.
/// </summary>
public class ExtractorPdfServiceTests
{
    private readonly IExtractorPdfService _servicio;

    public ExtractorPdfServiceTests()
    {
        var options = Options.Create(new AlmacenOptions
        {
            RutaAlmacenamiento = Path.Combine(Path.GetTempPath(), "siha-test-archivos")
        });

        _servicio = new ExtractorPdfService(options, NullLogger<ExtractorPdfService>.Instance);
    }

    [Fact]
    public async Task ExtraerDeTexto_documento_completo_genera_confianza_alta_y_estado_confirmada()
    {
        // Arrange (S2-P01: Extracción base con todos los datos)
        var texto = @"
            UNIVERSIDAD TÉCNICA DE AMBATO
            FACULTAD DE INGENIERÍA EN SISTEMAS
            Asignatura: Programación Estructurada
            Código: SW101
            Créditos: 4
            Horas totales: 120
            
            Unidad 1: Introducción a la algoritmia (20 horas)
            Unidad 2: Estructuras de control y bucles (30 horas)
            Unidad 3: Funciones y modularidad (30 horas)
            Unidad 4: Arreglos y punteros (40 horas)
        ";

        // Act
        var r = await _servicio.ExtraerDeTextoAsync(texto, tieneCapaTexto: true);

        // Assert
        r.Should().NotBeNull();
        r.NombreAsignaturaOrigen.Should().Be("Programación Estructurada");
        r.CodigoAsignaturaOrigen.Should().Be("SW101");
        r.Creditos.Should().Be(4);
        r.Horas.Should().Be(120);
        r.Unidades.Should().HaveCount(4);
        r.EsEscaneado.Should().BeFalse();
        r.Confianza.Should().BeGreaterOrEqualTo(EstadosExtraccion.UmbralConfianzaAuto); // >= 0.80
        r.Confianza.Should().Be(1.00m);
        r.EstadoSugerido.Should().Be(EstadosExtraccion.Confirmada);
        r.RequiereConfirmacionHumana.Should().BeFalse();
        r.CamposFaltantes.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtraerDeArchivo_lee_texto_de_un_pdf_y_no_incluye_operadores_pdf_en_los_datos()
    {
        using var temp = new DirectorioTemporal();
        var options = Options.Create(new AlmacenOptions { RutaAlmacenamiento = temp.Ruta });
        var servicio = new ExtractorPdfService(options, NullLogger<ExtractorPdfService>.Instance);
        var pdf = "%PDF-1.4\n1 0 obj\n<< /Length 300 >>\nstream\nBT\n(Asignatura: Estructuras de Datos) Tj\n(Codigo: SW201) Tj\n(Creditos: 4) Tj\n(Horas totales: 192) Tj\n(Unidad 1: Algoritmos - 32 horas.) Tj\n(Notacion O grande y complejidad temporal.) Tj\nET\nendstream\nendobj\n%%EOF";
        temp.CrearArchivo("plan.pdf", System.Text.Encoding.ASCII.GetBytes(pdf));

        var resultado = await servicio.ExtraerDeArchivoAsync("plan.pdf");

        resultado.CodigoAsignaturaOrigen.Should().Be("SW201");
        resultado.NombreAsignaturaOrigen.Should().Be("Estructuras de Datos");
        resultado.Creditos.Should().Be(4);
        resultado.Horas.Should().Be(192);
        resultado.Unidades.Should().ContainSingle().Which.Should().Match<UnidadExtraida>(u =>
            u.Nombre == "Algoritmos" && u.Horas == 32 && u.Contenidos.Contains("Notacion O grande"));
    }

    [Fact]
    public async Task ExtraerDeTexto_falta_creditos_reduce_confianza_y_marca_pendiente_confirmacion()
    {
        // Arrange (S2-P02: Bloqueo por baja confianza < 0.80)
        var texto = @"
            UNIVERSIDAD TÉCNICA DE AMBATO
            Asignatura: Base de Datos I
            Código: BD201
            Horas: 96
            
            Unidad 1: Modelo Relacional
            Unidad 2: SQL y Normalización
        ";

        // Act
        var r = await _servicio.ExtraerDeTextoAsync(texto, tieneCapaTexto: true);

        // Assert
        r.Creditos.Should().BeNull();
        r.Horas.Should().Be(96);
        r.Confianza.Should().BeLessThan(EstadosExtraccion.UmbralConfianzaAuto); // < 0.80
        r.EstadoSugerido.Should().Be(EstadosExtraccion.PendienteConfirmacion);
        r.RequiereConfirmacionHumana.Should().BeTrue();
        r.CamposFaltantes.Should().Contain("Creditos");
    }

    [Fact]
    public async Task ExtraerDeTexto_falta_horas_reduce_confianza_a_menos_de_0_80()
    {
        // Arrange
        var texto = @"
            Asignatura: Física General
            Código: FIS101
            Créditos: 3
            
            Unidad 1: Cinemática
        ";

        // Act
        var r = await _servicio.ExtraerDeTextoAsync(texto, tieneCapaTexto: true);

        // Assert
        r.Horas.Should().BeNull();
        r.Confianza.Should().BeLessThan(EstadosExtraccion.UmbralConfianzaAuto);
        r.EstadoSugerido.Should().Be(EstadosExtraccion.PendienteConfirmacion);
        r.RequiereConfirmacionHumana.Should().BeTrue();
        r.CamposFaltantes.Should().Contain("Horas");
    }

    [Fact]
    public async Task ExtraerDeTexto_sin_capa_texto_identifica_escaneado_y_deriva_a_pendiente_ocr_llm()
    {
        // Arrange (S2-P02 & CA3 US-07: PDF escaneado deriva a PENDIENTE_OCR_LLM sin inventar datos)
        var textoVacio = "";

        // Act
        var r = await _servicio.ExtraerDeTextoAsync(textoVacio, tieneCapaTexto: false, codigoAsignaturaFallback: "EXT-99");

        // Assert
        r.EsEscaneado.Should().BeTrue();
        r.EstadoSugerido.Should().Be(EstadosExtraccion.PendienteOcrLlm);
        r.Confianza.Should().Be(0.0m);
        r.Creditos.Should().BeNull();
        r.Horas.Should().BeNull();
        r.Unidades.Should().BeEmpty();
        r.RequiereConfirmacionHumana.Should().BeTrue();
        r.Observacion.Should().Contain("OCR");
    }

    [Fact]
    public async Task ExtraerDeTexto_ruido_binario_es_detectado_como_escaneado()
    {
        // Arrange (documento con bytes corruptos o sin texto alfabético reconocible)
        var textoRuido = "%%PDF-1.4 %âãÏÓ 1 0 obj <</Type /Catalog ... >> endobj 1234567890 0000000";

        // Act
        var r = await _servicio.ExtraerDeTextoAsync(textoRuido, tieneCapaTexto: true);

        // Assert
        r.EsEscaneado.Should().BeTrue();
        r.EstadoSugerido.Should().Be(EstadosExtraccion.PendienteOcrLlm);
        r.Confianza.Should().Be(0.0m);
    }

    [Fact]
    public void CalcularConfianza_suma_correctamente_ponderaciones()
    {
        // Arrange
        var unidades = new[] { new UnidadExtraida(1, "Tema 1", 10, "Contenido 1") };

        // Act & Assert
        // Todos presentes: 0.20 + 0.10 + 0.25 + 0.25 + 0.20 = 1.00
        _servicio.CalcularConfianza(4, 120, unidades, "Materia A", "MAT101")
            .Should().Be(1.00m);

        // Falta código (-0.10): 0.90
        _servicio.CalcularConfianza(4, 120, unidades, "Materia A", null)
            .Should().Be(0.90m);

        // Falta créditos (-0.25): 0.75 (< 0.80)
        _servicio.CalcularConfianza(null, 120, unidades, "Materia A", "MAT101")
            .Should().Be(0.75m);

        // Falta horas (-0.25): 0.75 (< 0.80)
        _servicio.CalcularConfianza(4, null, unidades, "Materia A", "MAT101")
            .Should().Be(0.75m);

        // Faltan unidades (-0.20): 0.80
        _servicio.CalcularConfianza(4, 120, Array.Empty<UnidadExtraida>(), "Materia A", "MAT101")
            .Should().Be(0.80m);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("1234567890!@#$%^&*()", true)]
    [InlineData("Texto válido con suficientes palabras legibles para un syllabus universitario completo.", false)]
    public void EsPdfEscaneado_evalua_criterio_de_legibilidad(string? texto, bool esperadoEscaneado)
    {
        var esEscaneado = _servicio.EsPdfEscaneado(texto);
        esEscaneado.Should().Be(esperadoEscaneado);
    }
}
