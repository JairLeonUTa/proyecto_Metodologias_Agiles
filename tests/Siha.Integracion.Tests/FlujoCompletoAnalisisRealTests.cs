extern alias msc;

using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Siha.Contracts;
using Siha.Contracts.Events;
using Siha.Integracion.Tests.Soporte;
using Xunit.Abstractions;
using ClienteGeneradoMsC = msc::Siha.MsA.Api.Grpc.ReglasGrpc.ReglasGrpcClient;
using ClienteReglasMsC = msc::Siha.MsC.Api.Grpc.ReglasGrpcClient;
using EstadosExtraccion = msc::Siha.MsC.Api.Domain.EstadosExtraccion;
using Extraccion = msc::Siha.MsC.Api.Domain.Entities.Extraccion;
using ExtraccionUnidad = msc::Siha.MsC.Api.Domain.Entities.ExtraccionUnidad;
using IExtractorPdfService = msc::Siha.MsC.Api.Services.IExtractorPdfService;
using IOllamaEmbeddingService = msc::Siha.MsC.Api.Services.IOllamaEmbeddingService;
using IPrecedenteService = msc::Siha.MsC.Api.Services.IPrecedenteService;
using IReglasGrpcClient = msc::Siha.MsC.Api.Grpc.IReglasGrpcClient;
using OpcionesReglasMsC = msc::Siha.MsC.Api.Options.ReglasGrpcOptions;
using SihaDbContextC = msc::Siha.MsC.Api.Data.SihaDbContextC;

namespace Siha.Integracion.Tests;

/// <summary>
/// S2-S09 (US-11) ampliado: recorrido completo del Sprint 2 con infraestructura real.
///
///   expediente → evento RabbitMQ → recepción idempotente en C → extracción del plan del volumen
///   → confirmación del operador (API) → reglas θ/δ/τ por gRPC desde el Microservicio A real
///   → Pcred → Pcont con embeddings (caché EMBEDDING_UNIDAD) → precedentes en DB C → estado final.
///
/// SQL Server, RabbitMQ, el servidor gRPC de A y la API de C son reales. Ollama se sustituye por un modelo
/// determinista (el CI no puede descargar modelos). El consumidor de C todavía no crea la fila EXTRACTION
/// por sí mismo, así que la prueba ejecuta ese paso con el ExtractorPdfService del propio C (ver evidencia).
/// </summary>
[Collection(ColeccionInfraestructura.Nombre)]
[Trait("Categoria", "InfraestructuraReal")]
public class FlujoCompletoAnalisisRealTests
{
    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(45);
    private readonly InfraestructuraReal _infra;
    private readonly ITestOutputHelper _salida;

    public FlujoCompletoAnalisisRealTests(InfraestructuraReal infra, ITestOutputHelper salida)
    {
        _infra = infra;
        _salida = salida;
    }

    private const string PlanOrigen = """
        UNIVERSIDAD DE PRUEBA - PLAN ANALÍTICO
        Asignatura: Estructuras de Datos
        Código: TI201
        Créditos: 4
        Horas totales: 192
        Unidad 1: Listas enlazadas (16 horas)
        Unidad 2: Pilas y colas (16 horas)
        Unidad 3: Árboles binarios (16 horas)
        """;

    [Fact]
    public async Task Expediente_recorre_todo_el_Sprint_2_hasta_quedar_listo_para_dictamen()
    {
        // ---- Microservicio A real (reglas por gRPC) ----
        await using var msa = new MsAReal(_infra);
        using var canal = msa.CanalGrpc();

        var volumen = Path.Combine(Path.GetTempPath(), "siha-integracion", Guid.NewGuid().ToString("N"));
        var ollama = new OllamaPorTemas();
        await using var msc = new MsCReal(_infra, volumen, servicios: s =>
        {
            s.RemoveAll<IOllamaEmbeddingService>();
            s.AddSingleton<IOllamaEmbeddingService>(ollama);
            s.RemoveAll<IReglasGrpcClient>();
            s.AddScoped<IReglasGrpcClient>(_ => new ClienteReglasMsC(
                new ClienteGeneradoMsC(canal),
                new OpcionesFijas(new OpcionesReglasMsC { TimeoutSegundos = 10, MaxRetries = 1, RetryBaseDelaySegundos = 0 }),
                NullLogger<ClienteReglasMsC>.Instance));
        });
        await msc.IniciarAsync();
        await using var publicador = await PublicadorPrueba.IniciarAsync(_infra);

        // ---- 1. Evento: el plan analítico llega por RabbitMQ como lo publica B ----
        var evento = EventoConPlan(volumen, Encoding.UTF8.GetBytes(PlanOrigen));
        await publicador.PublicarAsync(evento);
        (await msc.EsperarFilasAsync($"SELECT COUNT(*) FROM PROCESSED_EVENTS WHERE ExpedienteId = '{evento.ExpedienteId}'", 1, Espera))
            .Should().Be(1);
        _salida.WriteLine($"[1 evento] {evento.ExpedienteId} recibido en C");

        // ---- 2. Extracción del documento del volumen con el extractor de C ----
        var documento = evento.Documentos.Single();
        Guid extraccionId;
        using (var alcance = msc.Services.CreateScope())
        {
            var extractor = alcance.ServiceProvider.GetRequiredService<IExtractorPdfService>();
            var extraido = await extractor.ExtraerDeArchivoAsync(documento.RutaAlmacenamiento);
            extraido.CodigoAsignaturaOrigen.Should().Be("TI201");
            extraido.Creditos.Should().Be(4);
            extraido.Unidades.Should().HaveCount(3);

            var db = alcance.ServiceProvider.GetRequiredService<SihaDbContextC>();
            var extraccion = new Extraccion
            {
                ExpedienteId = evento.ExpedienteId,
                DocumentoId = documento.DocumentoId,
                CodigoAsignaturaOrigen = extraido.CodigoAsignaturaOrigen,
                NombreAsignaturaOrigen = extraido.NombreAsignaturaOrigen,
                Creditos = extraido.Creditos,
                Horas = extraido.Horas,
                Confianza = extraido.Confianza,
                Estado = EstadosExtraccion.PendienteConfirmacion, // CA de US-07: el operador revisa antes de evaluar
                Unidades = extraido.Unidades.Select(u => new ExtraccionUnidad
                {
                    Numero = u.Numero, Nombre = u.Nombre, Horas = u.Horas, Contenidos = u.Contenidos
                }).ToList()
            };
            db.Add(extraccion);
            await db.SaveChangesAsync();
            extraccionId = extraccion.Id;
            _salida.WriteLine($"[2 extracción] {extraido.CodigoAsignaturaOrigen} {extraido.Creditos} cr, {extraido.Unidades.Count} unidades, confianza {extraido.Confianza}");
        }

        var docente = msc.ClienteCon(RolesSistema.Docente);
        var solicitud = SolicitudAnalisis();

        // Sin confirmación no se evalúa.
        var previo = await LeerAsync(await docente.PostAsJsonAsync($"/api/extracciones/{extraccionId}/analizar", solicitud));
        previo.GetProperty("estadoFinal").GetInt32().Should().Be(5, "ExtraccionNoConfirmada");

        // ---- 3. Confirmación humana por la API de C ----
        var operador = msc.ClienteCon(RolesSistema.Operador);
        var confirmar = await operador.PutAsJsonAsync($"/api/extracciones/{extraccionId}/confirmar", new
        {
            creditos = 4,
            horas = 192,
            unidades = new[]
            {
                new { numero = 1, nombre = "Listas enlazadas", horas = 16, contenidos = "listas enlazadas simples y dobles" },
                new { numero = 2, nombre = "Pilas y colas", horas = 16, contenidos = "pilas y colas con arreglos" },
                new { numero = 3, nombre = "Árboles binarios", horas = 16, contenidos = "árboles binarios de búsqueda" }
            },
            observacion = "Revisado contra el PDF"
        });
        confirmar.StatusCode.Should().Be(HttpStatusCode.OK, await confirmar.Content.ReadAsStringAsync());

        // ---- 4. Precedente en DB C y caché de embeddings del plan destino (SW201) ----
        using (var alcance = msc.Services.CreateScope())
        {
            await alcance.ServiceProvider.GetRequiredService<IPrecedenteService>().RegistrarAsync(
                "RES-FISEI-2025-031", new[] { ("TI201", "Estructuras de Datos") }, "SW201", "Universidad de prueba",
                new DateOnly(2025, 6, 12));
        }

        var admin = msc.ClienteCon(RolesSistema.Administrador);
        var precarga = await admin.PostAsJsonAsync("/api/extracciones/embeddings/precalcular", new[]
        {
            new { codigoAsignatura = "SW201", unidades = UnidadesDestino.Select(u => new { numero = u.Numero, contenidos = u.Contenidos }) }
        });
        precarga.StatusCode.Should().Be(HttpStatusCode.OK, await precarga.Content.ReadAsStringAsync());
        (await LeerAsync(precarga)).GetProperty("generados").GetInt32().Should().Be(4);
        (await msc.ContarAsync("SELECT COUNT(*) FROM EMBEDDING_UNIDAD WHERE CodigoAsignatura = 'SW201'")).Should().Be(4);

        // El docente no puede precargar embeddings.
        (await docente.PostAsJsonAsync("/api/extracciones/embeddings/precalcular", Array.Empty<object>())).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);

        // ---- 5. Análisis completo ----
        var llamadasAntes = ollama.Llamadas;
        var respuesta = await docente.PostAsJsonAsync($"/api/extracciones/{extraccionId}/analizar", solicitud);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync());
        var r = await LeerAsync(respuesta);
        var traza = r.GetProperty("detalleTrazabilidad").GetString()!;
        _salida.WriteLine($"[5 análisis] {traza}");

        r.GetProperty("creditos").GetProperty("pcred").GetDecimal().Should().Be(100m, "4 / 4 créditos");
        var contenido = r.GetProperty("contenido");
        contenido.GetProperty("pcont").GetDecimal().Should().Be(75m, "3 de 4 unidades de 16 h cubiertas");
        contenido.GetProperty("unidadesFaltantes").EnumerateArray().Single().GetProperty("numero").GetInt32().Should().Be(4);
        contenido.GetProperty("tauUsado").GetDouble().Should().Be(75, "τ viene de la semilla de A por gRPC");
        r.GetProperty("precedentes").EnumerateArray().Single().GetProperty("resolucion").GetString().Should().Be("RES-FISEI-2025-031");
        r.GetProperty("estadoFinal").GetInt32().Should().Be(0, "ListaParaDictamen");
        traza.Should().Contain("gRPC Reglas OK").And.Contain("desde caché EMBEDDING_UNIDAD").And.Contain("Precedentes: 1");
        (ollama.Llamadas - llamadasAntes).Should().Be(3, "solo se generan las 3 unidades de origen; las 4 de destino salen de la caché");

        // ---- 6. Cambio de τ en A: la siguiente evaluación lo aplica sin reiniciar C ----
        var cambio = await msa.ClienteCon(RolesSistema.Administrador).PutAsJsonAsync(
            $"/api/reglas/parametros/{RutasNormativas.CambioCarreraOu}", new { theta = 80.0, delta = 15.0, tau = 99.0 });
        cambio.StatusCode.Should().Be(HttpStatusCode.OK, await cambio.Content.ReadAsStringAsync());

        var r2 = await LeerAsync(await docente.PostAsJsonAsync($"/api/extracciones/{extraccionId}/analizar", solicitud));
        r2.GetProperty("contenido").GetProperty("tauUsado").GetDouble().Should().Be(99);
        r2.GetProperty("contenido").GetProperty("pcont").GetDecimal().Should().Be(75m, "las coincidencias son exactas (similitud 100 %)");
        _salida.WriteLine($"[6 τ=99] Pcont={r2.GetProperty("contenido").GetProperty("pcont").GetDecimal()}");
    }

    [Fact]
    public async Task Con_el_Microservicio_A_caido_el_analisis_queda_en_ErrorReglasGrpc_sin_valores_inventados()
    {
        var volumen = Path.Combine(Path.GetTempPath(), "siha-integracion", Guid.NewGuid().ToString("N"));
        await using var msc = new MsCReal(_infra, volumen, urlReglasGrpc: "http://127.0.0.1:1", servicios: s =>
        {
            s.RemoveAll<IOllamaEmbeddingService>();
            s.AddSingleton<IOllamaEmbeddingService>(new OllamaPorTemas());
        });
        await msc.IniciarAsync();

        Guid extraccionId;
        using (var alcance = msc.Services.CreateScope())
        {
            var db = alcance.ServiceProvider.GetRequiredService<SihaDbContextC>();
            var extraccion = new Extraccion
            {
                ExpedienteId = Guid.NewGuid(),
                CodigoAsignaturaOrigen = "TI201",
                NombreAsignaturaOrigen = "Estructuras de Datos",
                Creditos = 4,
                Horas = 192,
                Estado = EstadosExtraccion.Confirmada,
                ConfirmadoPor = "qa-operador@uta.edu.ec",
                ConfirmadoUtc = DateTimeOffset.UtcNow,
                Unidades = new List<ExtraccionUnidad> { new() { Numero = 1, Nombre = "Listas", Horas = 16, Contenidos = "listas" } }
            };
            db.Add(extraccion);
            await db.SaveChangesAsync();
            extraccionId = extraccion.Id;
        }

        var reloj = System.Diagnostics.Stopwatch.StartNew();
        var respuesta = await msc.ClienteCon(RolesSistema.Docente).PostAsJsonAsync($"/api/extracciones/{extraccionId}/analizar", SolicitudAnalisis());
        reloj.Stop();

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        var r = await LeerAsync(respuesta);
        _salida.WriteLine($"[A caído] estado={r.GetProperty("estadoFinal").GetInt32()} en {reloj.ElapsedMilliseconds} ms");
        r.GetProperty("estadoFinal").GetInt32().Should().Be(4, "ErrorReglasGrpc");
        r.GetProperty("contenido").GetProperty("pcont").GetDecimal().Should().Be(0m);
        r.GetProperty("precedentes").GetArrayLength().Should().Be(0);
        reloj.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(60));
    }

    // ------------------------------------------------------------------ apoyo

    private static readonly (int Numero, string Nombre, string Contenidos)[] UnidadesDestino =
    {
        (1, "Listas", "listas enlazadas simples y dobles"),
        (2, "Pilas y colas", "pilas y colas con arreglos"),
        (3, "Árboles", "árboles binarios de búsqueda"),
        (4, "Grafos", "grafos y recorridos")
    };

    private static object SolicitudAnalisis() => new
    {
        anosTranscurridos = 2,
        numeroMatriculaDestino = 1,
        esCreditoExterno = false,
        rutaNormativa = RutasNormativas.CambioCarreraOu,
        codigoAsignaturaDestino = "SW201",
        institucionOrigen = "Universidad de prueba",
        creditosDestino = 4,
        // Sin vectores: C los toma de la caché EMBEDDING_UNIDAD (S2-S05).
        unidadesDestino = UnidadesDestino.Select(u => new { numero = u.Numero, nombre = u.Nombre, horas = 16, contenidos = u.Contenidos, embedding = Array.Empty<float>() }),
        validarRN19 = true
    };

    private static async Task<JsonElement> LeerAsync(HttpResponseMessage respuesta)
    {
        var texto = await respuesta.Content.ReadAsStringAsync();
        respuesta.IsSuccessStatusCode.Should().BeTrue(texto);
        return JsonDocument.Parse(texto).RootElement.Clone();
    }

    private static SolicitudCreadaEvent EventoConPlan(string volumen, byte[] contenido)
    {
        var expedienteId = Guid.NewGuid();
        var documentoId = Guid.NewGuid();
        var relativa = Path.Combine(expedienteId.ToString("N"), $"{documentoId:N}.pdf");
        var fisica = Path.Combine(volumen, relativa);
        Directory.CreateDirectory(Path.GetDirectoryName(fisica)!);
        File.WriteAllBytes(fisica, contenido);

        return new SolicitudCreadaEvent
        {
            ExpedienteId = expedienteId,
            CorrelationId = Guid.NewGuid(),
            CodigoExpediente = $"EXP-IT-{expedienteId:N}"[..20],
            TipoTramite = TiposTramite.Interno,
            RutaNormativa = RutasNormativas.CambioCarreraOu,
            CarreraOrigenCodigo = "TI",
            CarreraDestinoCodigo = "SW",
            DocumentoIds = new[] { documentoId },
            AsignaturasOrigen = new[] { "TI201" },
            Documentos = new[]
            {
                new DocumentoReferencia
                {
                    DocumentoId = documentoId,
                    RutaAlmacenamiento = relativa,
                    HashSha256 = Convert.ToHexString(SHA256.HashData(contenido)).ToLowerInvariant(),
                    TamanoBytes = contenido.Length
                }
            }
        };
    }

    /// <summary>
    /// Modelo de embeddings determinista: un eje por tema reconocido en el texto. Textos del mismo tema dan
    /// coseno 1 y de temas distintos coseno 0, así el resultado esperado se calcula a mano.
    /// </summary>
    private sealed class OllamaPorTemas : IOllamaEmbeddingService
    {
        private static readonly string[] Temas = { "lista", "pila", "árbol", "grafo" };
        private int _llamadas;
        public int Llamadas => _llamadas;

        public Task<float[]?> GenerarEmbeddingAsync(string texto, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _llamadas);
            var t = texto.ToLowerInvariant();
            var v = Temas.Select(tema => t.Contains(tema) ? 1f : 0f).ToArray();
            return Task.FromResult<float[]?>(v.Any(x => x > 0) ? v : null);
        }

        public Task<bool> EstaDisponibleAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class OpcionesFijas : IOptionsMonitor<OpcionesReglasMsC>
    {
        public OpcionesFijas(OpcionesReglasMsC valor) => CurrentValue = valor;
        public OpcionesReglasMsC CurrentValue { get; }
        public OpcionesReglasMsC Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<OpcionesReglasMsC, string?> listener) => null;
    }
}
