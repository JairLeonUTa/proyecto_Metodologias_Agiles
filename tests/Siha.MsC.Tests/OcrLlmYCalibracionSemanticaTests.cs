using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Siha.MsC.Api.Domain;
using Siha.MsC.Api.Options;
using Siha.MsC.Api.Services;

namespace Siha.MsC.Tests;

/// <summary>
/// Pruebas para S2-P05 (Motor OCR + prompt estructurado Ollama) y S2-P06 (Calibración de embeddings y evaluación semántica).
/// Responsable: Paul Acurio (PO Proxy & AI Lead).
/// </summary>
public class OcrLlmYCalibracionSemanticaTests
{
    private readonly OcrLlmExtractorService _ocrLlmService;
    private readonly IReglaContenidoStrategy _reglaContenido;

    public OcrLlmYCalibracionSemanticaTests()
    {
        var options = Options.Create(new OllamaOptions
        {
            Url = "http://localhost:11434",
            Modelo = "llama3.2:3b",
            ModeloEmbeddings = "nomic-embed-text",
            TimeoutSegundos = 10
        });

        _ocrLlmService = new OcrLlmExtractorService(
            new HttpClient(),
            options,
            NullLogger<OcrLlmExtractorService>.Instance);

        _reglaContenido = new ReglaContenidoStrategy();
    }

    #region S2-P05: Motor OCR + Prompt Anti-Alucinación

    [Fact]
    public void ConstruirPromptAntiAlucinacion_incluye_directivas_estrictas_de_cero_alucinacion()
    {
        // Arrange
        var textoOcr = "Universidad Central. Materia: Calculo Diferencial. 4 creditos.";

        // Act
        var prompt = _ocrLlmService.ConstruirPromptAntiAlucinacion(textoOcr);

        // Assert
        prompt.Should().Contain("CERO ALUCINACIÓN");
        prompt.Should().Contain("Jamás inventes valores");
        prompt.Should().Contain("debes responder estrictamente null");
        prompt.Should().Contain(textoOcr);
    }

    [Fact]
    public void ParsearRespuestaJsonOHeuristica_con_campos_dudosos_null_obtiene_confianza_menor_a_0_80_y_requiere_humano()
    {
        // Arrange (LLM devuelve JSON sin horas ni código porque eran ilegibles en el escaneado)
        var respuestaLlm = @"
        {
            ""codigo_asignatura"": null,
            ""nombre_asignatura"": ""Estructuras Discretas"",
            ""creditos"": 3,
            ""horas_totales"": null,
            ""unidades"": [
                { ""numero"": 1, ""nombre"": ""Lógica Proposicional"", ""horas"": 15, ""contenidos"": ""Tablas de verdad"" }
            ],
            ""confianza_estimada"": 0.65
        }";

        // Act
        var resultado = _ocrLlmService.ParsearRespuestaJsonOHeuristica(respuestaLlm, "texto escaneado", null);

        // Assert (CA1 US-10: campos dudosos null + confianza < 0.80 -> PENDIENTE_CONFIRMACION)
        resultado.NombreAsignaturaOrigen.Should().Be("Estructuras Discretas");
        resultado.Creditos.Should().Be(3);
        resultado.Horas.Should().BeNull();
        resultado.Confianza.Should().BeLessThan(EstadosExtraccion.UmbralConfianzaAuto);
        resultado.EstadoSugerido.Should().Be(EstadosExtraccion.PendienteConfirmacion);
        resultado.RequiereConfirmacionHumana.Should().BeTrue();
        resultado.CamposFaltantes.Should().Contain("Horas");
    }

    [Fact]
    public void ParsearRespuestaJsonOHeuristica_valores_fuera_de_rango_son_descartados_a_null()
    {
        // Arrange (LLM con alucinación de créditos excesivos o horas negativas)
        var respuestaAlucinada = @"
        {
            ""codigo_asignatura"": ""SW999"",
            ""nombre_asignatura"": ""Materia Rara"",
            ""creditos"": 150,
            ""horas_totales"": -50,
            ""unidades"": []
        }";

        // Act
        var resultado = _ocrLlmService.ParsearRespuestaJsonOHeuristica(respuestaAlucinada, "texto", null);

        // Assert
        resultado.Creditos.Should().BeNull("Créditos > 30 debe ser descartado como alucinación");
        resultado.Horas.Should().BeNull("Horas negativas debe ser descartado como alucinación");
        resultado.EstadoSugerido.Should().Be(EstadosExtraccion.PendienteConfirmacion);
        resultado.RequiereConfirmacionHumana.Should().BeTrue();
    }

    #endregion

    #region S2-P06: Calibración Semántica de Muestras Reales FISEI vs Sílabos Externos

    [Fact]
    public void Calibracion_planes_FISEI_asignaturas_homologas_superan_umbral_tau_0_75()
    {
        // Simulación calibrada de vectores normalizados (dim=4)
        // Fundamentos de Programación (FISEI SW) vs Programación I (Universidad Externa)
        // Ambos comparten enfoque algorítmico y estructuras
        var uOrigen = new[]
        {
            new UnidadOrigenEmbedding(1, "Algoritmos y Diagramas", 20, "Pseudocódigo y flujo", new float[] { 0.90f, 0.40f, 0.10f, 0.05f }),
            new UnidadOrigenEmbedding(2, "Estructuras de Control", 30, "Condicionales y ciclos", new float[] { 0.85f, 0.50f, 0.15f, 0.05f }),
            new UnidadOrigenEmbedding(3, "Modularidad y Funciones", 30, "Paso de parámetros", new float[] { 0.20f, 0.90f, 0.35f, 0.10f }),
            new UnidadOrigenEmbedding(4, "Arreglos Unidimensionales", 40, "Vectores e índices", new float[] { 0.15f, 0.40f, 0.90f, 0.10f })
        };

        var uDestino = new[]
        {
            new UnidadDestinoEmbedding(1, "Lógica de Programación", 20, "Flujogramas", new float[] { 0.92f, 0.38f, 0.08f, 0.05f }),
            new UnidadDestinoEmbedding(2, "Estructuras de Decisión e Iteración", 30, "Ciclos for y while", new float[] { 0.87f, 0.48f, 0.12f, 0.05f }),
            new UnidadDestinoEmbedding(3, "Subprogramas", 30, "Funciones y procedimientos", new float[] { 0.22f, 0.89f, 0.38f, 0.08f }),
            new UnidadDestinoEmbedding(4, "Estructuras Lineales Básicas", 40, "Arreglos y listas", new float[] { 0.18f, 0.38f, 0.91f, 0.08f })
        };

        var param = new ParametrosContenido(uOrigen, uDestino, Tau: 75.0, RutaNormativa: "CAU-P-011");

        // Act
        var evaluacion = _reglaContenido.Calcular(param);

        // Assert
        evaluacion.Pcont.Should().Be(100.00m, "todas las 4 unidades de programación correlacionan por encima de τ=75%");
        evaluacion.UnidadesCubiertas.Should().HaveCount(4);
        evaluacion.UnidadesFaltantes.Should().BeEmpty();
    }

    [Fact]
    public void Calibracion_asignaturas_dispares_fiseI_no_alcanzan_umbral_tau()
    {
        // Comparación entre 'Base de Datos' (Origen) y 'Física Clásica' (Destino)
        // Vectores ortogonales
        var uOrigen = new[]
        {
            new UnidadOrigenEmbedding(1, "Modelo Relacional", 30, "Tablas y claves", new float[] { 1.0f, 0.0f, 0.0f, 0.0f })
        };

        var uDestino = new[]
        {
            new UnidadDestinoEmbedding(1, "Cinemática y Dinámica", 40, "Movimiento rectilíneo", new float[] { 0.0f, 1.0f, 0.0f, 0.0f })
        };

        var param = new ParametrosContenido(uOrigen, uDestino, Tau: 75.0, RutaNormativa: "CAU-P-011");

        // Act
        var evaluacion = _reglaContenido.Calcular(param);

        // Assert
        evaluacion.Pcont.Should().Be(0.00m);
        evaluacion.UnidadesCubiertas.Should().BeEmpty();
        evaluacion.UnidadesFaltantes.Should().HaveCount(1);
        evaluacion.UnidadesFaltantes.Single().SimilitudMaxima.Should().Be(0.0);
    }

    #endregion
}
