using System.Diagnostics;
using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Siha.MsC.Api.Domain;
using Siha.MsC.Api.Options;
using Siha.MsC.Api.Services;

namespace Siha.MsC.Tests;

/// <summary>
/// S2-S07 (US-07 / US-10): estrés y tiempos límite del pipeline OCR + LLM y de los embeddings contra Ollama.
/// Se usa un servidor HTTP falso (HttpMessageHandler) que puede tardar, fallar o devolver basura, para medir
/// sin depender de un modelo real que el CI no puede descargar:
///   - un Ollama lento corta en TimeoutSegundos y no deja la petición colgada;
///   - un error o una respuesta corrupta nunca producen datos inventados;
///   - la cancelación del usuario se respeta;
///   - muchas peticiones simultáneas y planes de muchas páginas terminan con memoria acotada.
/// </summary>
public class OllamaEstresYTimeoutTests
{
    private const string RespuestaEmbedding = "{\"embedding\":[0.1,0.2,0.3,0.4]}";

    private static IOptions<OllamaOptions> Opciones(int timeoutSegundos = 2) => Microsoft.Extensions.Options.Options.Create(new OllamaOptions
    {
        Url = "http://ollama-falso:11434",
        Modelo = "llama3.2:3b",
        ModeloEmbeddings = "nomic-embed-text",
        TimeoutSegundos = timeoutSegundos
    });

    private static OllamaEmbeddingService Embeddings(ServidorFalso servidor, int timeoutSegundos = 2) =>
        new(new HttpClient(servidor), Opciones(timeoutSegundos), NullLogger<OllamaEmbeddingService>.Instance);

    private static OcrLlmExtractorService OcrLlm(ServidorFalso servidor, int timeoutSegundos = 2) =>
        new(new HttpClient(servidor), Opciones(timeoutSegundos), NullLogger<OcrLlmExtractorService>.Instance);

    /// <summary>Texto OCR de un plan analítico de <paramref name="paginas"/> páginas.</summary>
    private static string PlanOcr(int paginas)
    {
        var sb = new StringBuilder();
        sb.AppendLine("UNIVERSIDAD TÉCNICA DE AMBATO - PLAN ANALÍTICO");
        sb.AppendLine("Asignatura: Estructuras de Datos   Código: TI201   Créditos: 4   Horas: 192");
        for (var p = 1; p <= paginas; p++)
        {
            sb.AppendLine($"--- Página {p} ---");
            sb.AppendLine($"Unidad {p}: Tema {p} de estructuras lineales y no lineales, 16 horas.");
            sb.AppendLine(string.Join(' ', Enumerable.Repeat($"Contenido detallado de la página {p} con listas, pilas, colas y árboles.", 25)));
        }
        return sb.ToString();
    }

    // ---------------------------------------------------------------- embeddings

    [Fact]
    public async Task Embedding_con_Ollama_lento_corta_en_el_timeout_y_devuelve_null()
    {
        var servidor = ServidorFalso.ConRetraso(TimeSpan.FromSeconds(30), RespuestaEmbedding);
        var reloj = Stopwatch.StartNew();

        var v = await Embeddings(servidor, timeoutSegundos: 1).GenerarEmbeddingAsync("Listas y pilas");

        reloj.Stop();
        v.Should().BeNull("un timeout no puede convertirse en un vector inventado");
        reloj.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5), "corta en ~1 s, no espera los 30 s del servidor");
    }

    [Fact]
    public async Task Embedding_cancelado_por_el_usuario_propaga_la_cancelacion()
    {
        var servidor = ServidorFalso.ConRetraso(TimeSpan.FromSeconds(30), RespuestaEmbedding);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var accion = () => Embeddings(servidor, timeoutSegundos: 60).GenerarEmbeddingAsync("Listas", cts.Token);

        await accion.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "{\"error\":\"model not found\"}")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "")]
    [InlineData(HttpStatusCode.OK, "{\"embedding\":[]}")]
    [InlineData(HttpStatusCode.OK, "esto no es JSON")]
    [InlineData(HttpStatusCode.OK, "{\"otro\":1}")]
    public async Task Embedding_con_error_o_respuesta_corrupta_devuelve_null(HttpStatusCode codigo, string cuerpo)
    {
        var v = await Embeddings(ServidorFalso.Fijo(codigo, cuerpo)).GenerarEmbeddingAsync("Árboles");

        v.Should().BeNull();
    }

    [Fact]
    public async Task Embedding_con_Ollama_apagado_devuelve_null_sin_excepcion()
    {
        var v = await Embeddings(ServidorFalso.SinConexion()).GenerarEmbeddingAsync("Grafos");

        v.Should().BeNull();
    }

    [Fact]
    public async Task Doscientas_peticiones_de_embeddings_simultaneas_terminan_todas()
    {
        var servidor = ServidorFalso.ConRetraso(TimeSpan.FromMilliseconds(20), RespuestaEmbedding);
        var servicio = Embeddings(servidor, timeoutSegundos: 30);
        var reloj = Stopwatch.StartNew();

        var resultados = await Task.WhenAll(Enumerable.Range(0, 200).Select(i => servicio.GenerarEmbeddingAsync($"Unidad {i}")));

        reloj.Stop();
        resultados.Should().OnlyContain(v => v != null && v.Length == 4);
        servidor.Peticiones.Should().Be(200);
        servidor.MaximoSimultaneo.Should().BeGreaterThan(1, "el cliente no serializa las llamadas");
        reloj.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task Con_un_Ollama_que_falla_una_de_cada_tres_veces_no_se_mezclan_vectores()
    {
        var servidor = ServidorFalso.Alternante(cadaN: 3, RespuestaEmbedding);
        var servicio = Embeddings(servidor);

        var resultados = await Task.WhenAll(Enumerable.Range(0, 30).Select(i => servicio.GenerarEmbeddingAsync($"U{i}")));

        resultados.Count(v => v is null).Should().Be(10);
        resultados.Where(v => v is not null).Should().OnlyContain(v => v!.SequenceEqual(new[] { 0.1f, 0.2f, 0.3f, 0.4f }));
    }

    // ---------------------------------------------------------------- OCR + LLM

    [Fact]
    public async Task OcrLlm_con_Ollama_lento_corta_y_cae_al_parser_de_respaldo_que_pide_confirmacion()
    {
        var servidor = ServidorFalso.ConRetraso(TimeSpan.FromSeconds(30), "{\"response\":\"{}\"}");
        var reloj = Stopwatch.StartNew();

        var r = await OcrLlm(servidor, timeoutSegundos: 1).ExtraerConLlmAsync(PlanOcr(3), "TI201");

        reloj.Stop();
        reloj.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
        r.RequiereConfirmacionHumana.Should().BeTrue("sin respuesta del modelo el operador debe revisar");
        r.Confianza.Should().BeLessThan(1m);
    }

    [Fact]
    public async Task OcrLlm_con_respuesta_basura_no_inventa_creditos_ni_horas_fuera_de_rango()
    {
        var basura = "{\"response\":\"{\\\"creditos\\\": 999, \\\"horas\\\": -5, \\\"unidades\\\": \\\"no\\\"}\"}";

        var r = await OcrLlm(ServidorFalso.Fijo(HttpStatusCode.OK, basura)).ExtraerConLlmAsync(PlanOcr(2), "TI201");

        r.Creditos.Should().NotBe(999);
        r.Horas.Should().NotBe(-5);
        r.RequiereConfirmacionHumana.Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(40)]
    public async Task OcrLlm_con_planes_de_varias_paginas_envia_todo_el_texto_en_una_llamada(int paginas)
    {
        var servidor = ServidorFalso.Fijo(HttpStatusCode.OK, "{\"response\":\"{}\"}");
        var texto = PlanOcr(paginas);

        var r = await OcrLlm(servidor).ExtraerConLlmAsync(texto, "TI201");

        r.Should().NotBeNull();
        servidor.Peticiones.Should().Be(1);
        servidor.UltimoCuerpo.Should().Contain($"gina {paginas} ---", "la última página llega al modelo");
        servidor.UltimoCuerpo!.Length.Should().BeGreaterThan(texto.Length / 2);
    }

    [Fact]
    public async Task OcrLlm_cincuenta_extracciones_seguidas_no_hacen_crecer_la_memoria()
    {
        var servidor = ServidorFalso.Fijo(HttpStatusCode.OK, "{\"response\":\"{}\"}");
        var servicio = OcrLlm(servidor);
        var texto = PlanOcr(40); // ~200 KB por documento
        await servicio.ExtraerConLlmAsync(texto, "TI201"); // calentamiento

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var antes = GC.GetTotalMemory(forceFullCollection: true);
        for (var i = 0; i < 50; i++)
            await servicio.ExtraerConLlmAsync(texto, "TI201");
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var despues = GC.GetTotalMemory(forceFullCollection: true);

        var crecimientoMb = (despues - antes) / (1024.0 * 1024.0);
        crecimientoMb.Should().BeLessThan(20, $"50 documentos de {texto.Length / 1024} KB no deben quedar retenidos");
    }

    [Fact]
    public async Task OcrLlm_veinte_extracciones_simultaneas_con_Ollama_lento_no_superan_el_timeout()
    {
        var servidor = ServidorFalso.ConRetraso(TimeSpan.FromSeconds(30), "{\"response\":\"{}\"}");
        var servicio = OcrLlm(servidor, timeoutSegundos: 1);
        var reloj = Stopwatch.StartNew();

        var resultados = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => servicio.ExtraerConLlmAsync(PlanOcr(5), "TI201")));

        reloj.Stop();
        resultados.Should().OnlyContain(r => r.RequiereConfirmacionHumana);
        reloj.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(8), "todas cortan a la vez, no una detrás de otra");
    }

    [Fact]
    public async Task OcrLlm_texto_vacio_no_llama_al_modelo()
    {
        var servidor = ServidorFalso.Fijo(HttpStatusCode.OK, "{\"response\":\"{}\"}");

        var r = await OcrLlm(servidor).ExtraerConLlmAsync("   ", "TI201");

        servidor.Peticiones.Should().Be(0);
        r.EstadoSugerido.Should().Be(EstadosExtraccion.PendienteOcrLlm);
    }

    /// <summary>Servidor HTTP en memoria con retraso, fallos programados y contadores de concurrencia.</summary>
    private sealed class ServidorFalso : HttpMessageHandler
    {
        private readonly Func<int, CancellationToken, Task<HttpResponseMessage>> _responder;
        private int _peticiones;
        private int _enCurso;
        private int _maximo;

        private ServidorFalso(Func<int, CancellationToken, Task<HttpResponseMessage>> responder) => _responder = responder;

        public int Peticiones => _peticiones;
        public int MaximoSimultaneo => _maximo;
        public string? UltimoCuerpo { get; private set; }

        private static HttpResponseMessage Respuesta(HttpStatusCode codigo, string cuerpo) =>
            new(codigo) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") };

        public static ServidorFalso Fijo(HttpStatusCode codigo, string cuerpo) =>
            new((_, _) => Task.FromResult(Respuesta(codigo, cuerpo)));

        public static ServidorFalso ConRetraso(TimeSpan retraso, string cuerpo) =>
            new(async (_, ct) =>
            {
                await Task.Delay(retraso, ct);
                return Respuesta(HttpStatusCode.OK, cuerpo);
            });

        public static ServidorFalso SinConexion() =>
            new((_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("Connection refused (ollama:11434)")));

        public static ServidorFalso Alternante(int cadaN, string cuerpo) =>
            new((n, _) => Task.FromResult(n % cadaN == 0
                ? Respuesta(HttpStatusCode.InternalServerError, "{\"error\":\"out of memory\"}")
                : Respuesta(HttpStatusCode.OK, cuerpo)));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var n = Interlocked.Increment(ref _peticiones);
            var actuales = Interlocked.Increment(ref _enCurso);
            int previo;
            while (actuales > (previo = _maximo) && Interlocked.CompareExchange(ref _maximo, actuales, previo) != previo) { }

            try
            {
                if (request.Content is not null)
                    UltimoCuerpo = await request.Content.ReadAsStringAsync(cancellationToken);
                return await _responder(n, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref _enCurso);
            }
        }
    }
}
