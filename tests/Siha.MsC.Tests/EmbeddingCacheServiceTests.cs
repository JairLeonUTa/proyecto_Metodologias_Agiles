using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Siha.MsA.Api.Grpc;
using Siha.MsC.Api.Contracts.Dtos;
using Siha.MsC.Api.Data;
using Siha.MsC.Api.Domain;
using Siha.MsC.Api.Domain.Entities;
using Siha.MsC.Api.Grpc;
using Siha.MsC.Api.Options;
using Siha.MsC.Api.Services;

namespace Siha.MsC.Tests;

/// <summary>
/// S2-S05 (US-10): caché de embeddings EMBEDDING_UNIDAD en DB C.
/// Un vector se pide a Ollama una sola vez por unidad y texto; si el plan cambia se recalcula, y si Ollama
/// no responde no se guarda ni se inventa ningún vector. La versión con SQL Server real está en
/// Siha.Integracion.Tests (PrecedentesYCacheRealTests).
/// </summary>
public class EmbeddingCacheServiceTests
{
    private static SihaDbContextC NuevaDb() => new(new DbContextOptionsBuilder<SihaDbContextC>()
        .UseInMemoryDatabase($"SihaDbC_S05_{Guid.NewGuid():N}").Options);

    private static IOptions<OllamaOptions> Opciones(string modelo = "nomic-embed-text") =>
        Microsoft.Extensions.Options.Options.Create(new OllamaOptions { ModeloEmbeddings = modelo });

    private static EmbeddingCacheService Cache(SihaDbContextC db, IOllamaEmbeddingService ollama, string modelo = "nomic-embed-text") =>
        new(db, ollama, Opciones(modelo), NullLogger<EmbeddingCacheService>.Instance);

    [Fact]
    public async Task Primera_consulta_genera_el_vector_y_lo_guarda_en_EMBEDDING_UNIDAD()
    {
        using var db = NuevaDb();
        var ollama = new OllamaContador();

        var v = await Cache(db, ollama).ObtenerAsync("sw201", 1, "Listas y pilas");

        v.Should().Equal(OllamaContador.VectorDe("Listas y pilas"));
        ollama.Llamadas.Should().Be(1);
        var fila = await db.EmbeddingsUnidad.SingleAsync();
        fila.CodigoAsignatura.Should().Be("SW201", "el código se normaliza a mayúsculas");
        fila.NumeroUnidad.Should().Be(1);
        fila.Modelo.Should().Be("nomic-embed-text");
        fila.Dimensiones.Should().Be(4);
        fila.HashContenido.Should().HaveLength(64);
    }

    [Fact]
    public async Task Segunda_consulta_sale_de_la_cache_sin_llamar_a_Ollama()
    {
        using var db = NuevaDb();
        var ollama = new OllamaContador();
        var cache = Cache(db, ollama);

        var primera = await cache.ObtenerAsync("SW201", 1, "Listas y pilas");
        var segunda = await cache.ObtenerAsync("SW201", 1, "  Listas y pilas  ");

        segunda.Should().Equal(primera!);
        ollama.Llamadas.Should().Be(1, "los espacios de los extremos no cambian el texto normalizado");
    }

    [Fact]
    public async Task El_vector_se_recupera_bit_a_bit_igual()
    {
        using var db = NuevaDb();
        var original = new[] { 0.1f, -2.5e-7f, float.Epsilon, 123456.78f, -0.0f, 1f / 3f };
        var ollama = new Mock<IOllamaEmbeddingService>();
        ollama.Setup(o => o.GenerarEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(original);
        var cache = Cache(db, ollama.Object);
        await cache.ObtenerAsync("SW101", 2, "Bucles");

        var recuperado = await Cache(db, Mock.Of<IOllamaEmbeddingService>()).ObtenerAsync("SW101", 2, "Bucles");

        recuperado.Should().HaveCount(original.Length);
        for (var i = 0; i < original.Length; i++)
            BitConverter.SingleToInt32Bits(recuperado![i]).Should().Be(BitConverter.SingleToInt32Bits(original[i]));
    }

    [Fact]
    public async Task Si_cambia_el_texto_del_plan_se_recalcula_y_se_reemplaza_la_fila()
    {
        using var db = NuevaDb();
        var ollama = new OllamaContador();
        var cache = Cache(db, ollama);
        await cache.ObtenerAsync("SW201", 1, "Listas y pilas");

        var nuevo = await cache.ObtenerAsync("SW201", 1, "Listas, pilas y colas");

        nuevo.Should().Equal(OllamaContador.VectorDe("Listas, pilas y colas"));
        ollama.Llamadas.Should().Be(2);
        (await db.EmbeddingsUnidad.CountAsync()).Should().Be(1, "sigue habiendo una fila por unidad y modelo");
    }

    [Fact]
    public async Task Otro_modelo_de_embeddings_no_reutiliza_vectores_de_un_modelo_distinto()
    {
        using var db = NuevaDb();
        var ollama = new OllamaContador();
        await Cache(db, ollama, "nomic-embed-text").ObtenerAsync("SW201", 1, "Árboles");

        await Cache(db, ollama, "mxbai-embed-large").ObtenerAsync("SW201", 1, "Árboles");

        ollama.Llamadas.Should().Be(2);
        (await db.EmbeddingsUnidad.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Con_Ollama_caido_devuelve_null_y_no_guarda_nada()
    {
        using var db = NuevaDb();
        var ollama = new Mock<IOllamaEmbeddingService>();
        ollama.Setup(o => o.GenerarEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((float[]?)null);

        var v = await Cache(db, ollama.Object).ObtenerAsync("SW201", 1, "Grafos");

        v.Should().BeNull("sin modelo no se inventa un vector");
        (await db.EmbeddingsUnidad.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Con_Ollama_caido_la_cache_existente_sigue_sirviendo()
    {
        using var db = NuevaDb();
        await Cache(db, new OllamaContador()).ObtenerAsync("SW201", 3, "Grafos");
        var caido = new Mock<IOllamaEmbeddingService>(MockBehavior.Strict);

        var v = await Cache(db, caido.Object).ObtenerAsync("SW201", 3, "Grafos");

        v.Should().Equal(OllamaContador.VectorDe("Grafos"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Unidad_sin_contenidos_no_llama_a_Ollama(string contenidos)
    {
        using var db = NuevaDb();
        var ollama = new OllamaContador();

        (await Cache(db, ollama).ObtenerAsync("SW201", 1, contenidos)).Should().BeNull();
        ollama.Llamadas.Should().Be(0);
    }

    [Fact]
    public async Task Codigo_de_asignatura_vacio_se_rechaza()
    {
        using var db = NuevaDb();
        var accion = () => Cache(db, new OllamaContador()).ObtenerAsync(" ", 1, "Texto");

        await accion.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Precarga_de_los_43_planes_de_Software_y_segunda_pasada_desde_cache()
    {
        using var db = NuevaDb();
        var ollama = new OllamaContador();
        var cache = Cache(db, ollama);
        // 43 asignaturas × 4 unidades, como el catálogo completo de Software.
        var unidades = Enumerable.Range(1, 43)
            .SelectMany(a => Enumerable.Range(1, 4).Select(u => new UnidadPlanEmbedding($"SW{a:000}", u, $"Contenido {a}-{u}")))
            .ToList();

        var primera = await cache.PrecalcularAsync(unidades);
        var segunda = await cache.PrecalcularAsync(unidades);

        primera.Should().Be(new ResultadoPrecalculoEmbeddings(172, 0, 172, 0));
        segunda.Should().Be(new ResultadoPrecalculoEmbeddings(172, 172, 0, 0));
        ollama.Llamadas.Should().Be(172, "la segunda pasada no vuelve a llamar al modelo");
        (await db.EmbeddingsUnidad.CountAsync()).Should().Be(172);
    }

    [Fact]
    public async Task Precarga_cuenta_las_unidades_fallidas_sin_detenerse()
    {
        using var db = NuevaDb();
        var ollama = new Mock<IOllamaEmbeddingService>();
        ollama.Setup(o => o.GenerarEmbeddingAsync(It.Is<string>(t => t.Contains("falla")), It.IsAny<CancellationToken>()))
            .ReturnsAsync((float[]?)null);
        ollama.Setup(o => o.GenerarEmbeddingAsync(It.Is<string>(t => !t.Contains("falla")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 1f, 0f });

        var r = await Cache(db, ollama.Object).PrecalcularAsync(new[]
        {
            new UnidadPlanEmbedding("SW101", 1, "ok"),
            new UnidadPlanEmbedding("SW101", 2, "esta falla"),
            new UnidadPlanEmbedding("SW101", 3, "ok también")
        });

        r.Should().Be(new ResultadoPrecalculoEmbeddings(3, 0, 2, 1));
    }

    [Fact]
    public async Task El_analizador_toma_de_la_cache_las_unidades_destino_que_llegan_sin_vector()
    {
        using var db = NuevaDb();
        var extraccionId = Guid.NewGuid();
        db.Extracciones.Add(new Extraccion
        {
            Id = extraccionId,
            ExpedienteId = Guid.NewGuid(),
            CodigoAsignaturaOrigen = "TI201",
            NombreAsignaturaOrigen = "Estructuras",
            Creditos = 4,
            Horas = 192,
            Estado = EstadosExtraccion.Confirmada,
            ConfirmadoPor = "operador@uta.edu.ec",
            ConfirmadoUtc = DateTimeOffset.UtcNow,
            Unidades = new List<ExtraccionUnidad>
            {
                new() { Numero = 1, Nombre = "Listas", Horas = 16, Contenidos = "Listas y pilas" },
                new() { Numero = 2, Nombre = "Árboles", Horas = 16, Contenidos = "Árboles binarios" }
            }
        });
        await db.SaveChangesAsync();

        var ollama = new OllamaContador();
        var cache = Cache(db, ollama);
        await cache.PrecalcularAsync(new[]
        {
            new UnidadPlanEmbedding("SW201", 1, "Listas y pilas"),
            new UnidadPlanEmbedding("SW201", 2, "Árboles binarios")
        });
        var llamadasTrasPrecarga = ollama.Llamadas;

        var grpc = new Mock<IReglasGrpcClient>();
        grpc.Setup(g => g.ObtenerConfiguracionAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfiguracionRuta
            {
                ParametrosConfigurados = true,
                Parametros = new ParametrosEvaluacionMensaje { Theta = 80, Delta = 15, Tau = 75, Version = 1 }
            });

        var agente = new AgenteAnalizadorService(db, new ExtraccionService(db), grpc.Object, new ReglaCreditosStrategy(),
            new ReglaContenidoStrategy(), new PrecedenteService(db), ollama, NullLogger<AgenteAnalizadorService>.Instance, cache);

        var r = await agente.AnalizarAsync(extraccionId, new ParametrosAnalisisCompleto(
            2, 1, false, "CAU-P-011", "SW201", "UTA", 4, new[]
            {
                new UnidadDestinoEmbeddingRequest(1, "Listas", 16, "Listas y pilas", Array.Empty<float>()),
                new UnidadDestinoEmbeddingRequest(2, "Árboles", 16, "Árboles binarios", Array.Empty<float>())
            }));

        r.Contenido.Pcont.Should().Be(100m);
        r.DetalleTrazabilidad.Should().Contain("desde caché EMBEDDING_UNIDAD");
        (ollama.Llamadas - llamadasTrasPrecarga).Should().Be(2, "solo se generan las 2 unidades de origen; las de destino salen de la caché");
    }

    [Fact]
    public async Task Sin_cache_ni_Ollama_el_analizador_no_inventa_el_vector_de_destino()
    {
        using var db = NuevaDb();
        var extraccionId = Guid.NewGuid();
        db.Extracciones.Add(new Extraccion
        {
            Id = extraccionId,
            CodigoAsignaturaOrigen = "TI201",
            NombreAsignaturaOrigen = "Estructuras",
            Creditos = 4,
            Estado = EstadosExtraccion.Confirmada,
            Unidades = new List<ExtraccionUnidad> { new() { Numero = 1, Nombre = "Listas", Horas = 16, Contenidos = "Listas" } }
        });
        await db.SaveChangesAsync();

        var ollamaOrigen = new OllamaContador();
        var caido = new Mock<IOllamaEmbeddingService>();
        caido.Setup(o => o.GenerarEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((float[]?)null);
        var grpc = new Mock<IReglasGrpcClient>();
        grpc.Setup(g => g.ObtenerConfiguracionAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfiguracionRuta { ParametrosConfigurados = false });

        var agente = new AgenteAnalizadorService(db, new ExtraccionService(db), grpc.Object, new ReglaCreditosStrategy(),
            new ReglaContenidoStrategy(), new PrecedenteService(db), ollamaOrigen, NullLogger<AgenteAnalizadorService>.Instance,
            Cache(db, caido.Object));

        var accion = () => agente.AnalizarAsync(extraccionId, new ParametrosAnalisisCompleto(
            2, 1, false, "CAU-P-011", "SW201", "UTA", 4,
            new[] { new UnidadDestinoEmbeddingRequest(1, "Listas", 16, "Listas", Array.Empty<float>()) }));

        // El controlador traduce ArgumentException a 400 con el motivo.
        (await accion.Should().ThrowAsync<ArgumentException>()).WithMessage("*unidad destino 1*Ollama no respondió*");
        (await db.EmbeddingsUnidad.CountAsync()).Should().Be(0);
    }

    /// <summary>Ollama falso: vector determinista a partir del texto y contador de llamadas.</summary>
    private sealed class OllamaContador : IOllamaEmbeddingService
    {
        private int _llamadas;
        public int Llamadas => _llamadas;

        public static float[] VectorDe(string texto)
        {
            var h = (uint)texto.Trim().Aggregate(17, (a, c) => unchecked(a * 31 + c));
            return new[] { 1f, (h % 97) / 97f, (h % 89) / 89f, (h % 83) / 83f };
        }

        public Task<float[]?> GenerarEmbeddingAsync(string texto, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _llamadas);
            return Task.FromResult<float[]?>(VectorDe(texto));
        }

        public Task<bool> EstaDisponibleAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
