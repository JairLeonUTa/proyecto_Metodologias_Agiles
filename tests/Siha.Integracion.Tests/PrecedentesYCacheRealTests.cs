extern alias msc;

using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Siha.Integracion.Tests.Soporte;
using Xunit.Abstractions;
using BuscarPrecedenteRequest = msc::Siha.MsC.Api.Contracts.Dtos.BuscarPrecedenteRequest;
using EmbeddingCacheService = msc::Siha.MsC.Api.Services.EmbeddingCacheService;
using IOllamaEmbeddingService = msc::Siha.MsC.Api.Services.IOllamaEmbeddingService;
using OllamaOptions = msc::Siha.MsC.Api.Options.OllamaOptions;
using PrecedenteService = msc::Siha.MsC.Api.Services.PrecedenteService;
using SihaDbContextC = msc::Siha.MsC.Api.Data.SihaDbContextC;

namespace Siha.Integracion.Tests;

/// <summary>
/// S2-S08 (US-11) y S2-S05 (US-10) con SQL Server 2022 REAL (DB C migrada por el Program.cs del Microservicio C).
///
/// Precedentes: coincidencia exacta con la intercalación real de SQL Server, consulta vacía → [], nada
/// "parecido" y, si la base no responde, error explícito en vez de una lista vacía que parezca "sin precedentes".
/// Caché de embeddings: el índice único de EMBEDDING_UNIDAD resuelve las escrituras simultáneas y el vector
/// varbinary vuelve intacto.
/// </summary>
[Collection(ColeccionInfraestructura.Nombre)]
[Trait("Categoria", "InfraestructuraReal")]
public class PrecedentesYCacheRealTests
{
    private readonly InfraestructuraReal _infra;
    private readonly ITestOutputHelper _salida;

    public PrecedentesYCacheRealTests(InfraestructuraReal infra, ITestOutputHelper salida)
    {
        _infra = infra;
        _salida = salida;
    }

    private static string NuevoVolumen() =>
        Path.Combine(Path.GetTempPath(), "siha-integracion", Guid.NewGuid().ToString("N"));

    private static SihaDbContextC Db(string cadena) =>
        new(new DbContextOptionsBuilder<SihaDbContextC>().UseSqlServer(cadena).Options);

    private static async Task<MsCReal> MsCMigradoAsync(InfraestructuraReal infra)
    {
        var msc = new MsCReal(infra, NuevoVolumen());
        await msc.IniciarAsync();
        return msc;
    }

    [Fact]
    public async Task Todas_las_migraciones_de_DB_C_se_aplican_en_orden_sobre_una_base_vacia()
    {
        await using var msc = await MsCMigradoAsync(_infra);

        var esperadas = typeof(SihaDbContextC).Assembly.GetTypes()
            .Count(t => t.IsSubclassOf(typeof(Microsoft.EntityFrameworkCore.Migrations.Migration)));
        (await msc.ContarAsync("SELECT COUNT(*) FROM __EFMigrationsHistory")).Should().Be(esperadas);
        foreach (var tabla in new[] { "PRECEDENTE", "EMBEDDING_UNIDAD", "ANALISIS_EXPEDIENTE", "DICTAMEN_DOCENTE" })
        {
            (await msc.ContarAsync($"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{tabla}'"))
                .Should().Be(1, $"la tabla {tabla} debe existir");
        }

        (await msc.ContarAsync(
            "SELECT COUNT(*) FROM sys.indexes WHERE name = 'IX_EMBEDDING_UNIDAD_CodigoAsignatura_NumeroUnidad_Modelo' AND is_unique = 1"))
            .Should().Be(1);
        _salida.WriteLine($"[DB C real] {msc.BaseDatos}: {esperadas} migraciones aplicadas, tablas del Sprint 2 y de solicitudes SIHA creadas.");
    }

    [Fact]
    public async Task Precedente_registrado_se_encuentra_por_coincidencia_exacta_sin_importar_orden_ni_mayusculas()
    {
        await using var msc = await MsCMigradoAsync(_infra);
        await using var db = Db(msc.CadenaConexion);
        var servicio = new PrecedenteService(db);
        var id = await servicio.RegistrarAsync("RES-FISEI-2025-031",
            new[] { ("TI101", "Programación I"), ("TI102", "Programación II") }, "SW101", "Universidad de prueba",
            new DateOnly(2025, 6, 12));

        var r = await servicio.BuscarAsync(new BuscarPrecedenteRequest(new[] { " ti102", "TI101 " }, "sw101", "UNIVERSIDAD DE PRUEBA"));

        r.Should().ContainSingle();
        var p = r.Single();
        p.Id.Should().Be(id);
        p.Resolucion.Should().Be("RES-FISEI-2025-031");
        p.AsignaturasOrigen.Select(a => a.Codigo).Should().Equal("TI101", "TI102");
        _salida.WriteLine($"[Precedente real] {p.Resolucion} {p.AsignaturaDestino} {p.Fecha}");
    }

    [Fact]
    public async Task Con_la_tabla_vacia_la_busqueda_devuelve_lista_vacia()
    {
        await using var msc = await MsCMigradoAsync(_infra);
        await using var db = Db(msc.CadenaConexion);

        var r = await new PrecedenteService(db).BuscarAsync(new BuscarPrecedenteRequest(new[] { "TI101" }, "SW101", "UTA"));

        r.Should().BeEmpty();
    }

    [Theory]
    [InlineData(new[] { "TI101" }, "SW101", "Universidad de prueba")]                    // falta una asignatura de origen
    [InlineData(new[] { "TI101", "TI102", "TI103" }, "SW101", "Universidad de prueba")]  // sobra una
    [InlineData(new[] { "TI101", "TI102" }, "SW102", "Universidad de prueba")]           // otro destino
    [InlineData(new[] { "TI101", "TI102" }, "SW101", "Universidad de pruebas")]          // otra institución
    [InlineData(new string[0], "SW101", "Universidad de prueba")]                        // consulta vacía
    public async Task Lo_parecido_no_cuenta_como_precedente(string[] origen, string destino, string institucion)
    {
        await using var msc = await MsCMigradoAsync(_infra);
        await using var db = Db(msc.CadenaConexion);
        var servicio = new PrecedenteService(db);
        await servicio.RegistrarAsync("RES-FISEI-2025-031",
            new[] { ("TI101", "Programación I"), ("TI102", "Programación II") }, "SW101", "Universidad de prueba",
            new DateOnly(2025, 6, 12));

        var r = await servicio.BuscarAsync(new BuscarPrecedenteRequest(origen, destino, institucion));

        r.Should().BeEmpty("no se inventan referencias por similitud");
    }

    [Fact]
    public async Task Fila_con_JSON_corrupto_se_ignora_sin_romper_la_busqueda()
    {
        await using var msc = await MsCMigradoAsync(_infra);
        await using (var conexion = new SqlConnection(msc.CadenaConexion))
        {
            await conexion.OpenAsync();
            await using var cmd = new SqlCommand(
                "INSERT INTO PRECEDENTE (Id, Resolucion, AsignaturasOrigenJson, AsignaturaDestino, InstitucionOrigen, Fecha, CreadoUtc) " +
                "VALUES (NEWID(), 'RES-ROTO', '{no es json', 'SW101', 'UTA', '2025-01-01', SYSDATETIMEOFFSET())", conexion);
            await cmd.ExecuteNonQueryAsync();
        }

        await using var db = Db(msc.CadenaConexion);
        var r = await new PrecedenteService(db).BuscarAsync(new BuscarPrecedenteRequest(new[] { "TI101" }, "SW101", "UTA"));

        r.Should().BeEmpty();
    }

    [Fact]
    public async Task Si_la_base_no_responde_la_busqueda_falla_en_vez_de_decir_que_no_hay_precedentes()
    {
        // Puerto sin servidor: equivale a que el contenedor de DB C esté detenido.
        await using var db = Db("Server=127.0.0.1,1;Database=SihaDbC;User Id=sa;Password=x;TrustServerCertificate=True;Connect Timeout=3");
        var reloj = System.Diagnostics.Stopwatch.StartNew();

        var accion = () => new PrecedenteService(db).BuscarAsync(new BuscarPrecedenteRequest(new[] { "TI101" }, "SW101", "UTA"));

        var ex = await accion.Should().ThrowAsync<Exception>();
        reloj.Stop();
        _salida.WriteLine($"[DB C caída] {ex.Which.GetType().Name}: {ex.Which.Message.Split('\n')[0]} ({reloj.ElapsedMilliseconds} ms)");
        ex.Which.Should().Match<Exception>(e => e is SqlException || e.InnerException is SqlException || e is InvalidOperationException);
        reloj.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30), "no se queda colgado");
    }

    [Fact]
    public async Task Cache_de_embeddings_escrituras_simultaneas_de_la_misma_unidad_dejan_una_fila()
    {
        await using var msc = await MsCMigradoAsync(_infra);
        var ollama = new OllamaLento();
        var opciones = Microsoft.Extensions.Options.Options.Create(new OllamaOptions { ModeloEmbeddings = "nomic-embed-text" });

        // Diez peticiones a la vez, cada una con su propio DbContext (como diez requests HTTP).
        var tareas = Enumerable.Range(0, 10).Select(async _ =>
        {
            await using var db = Db(msc.CadenaConexion);
            return await new EmbeddingCacheService(db, ollama, opciones, NullLogger<EmbeddingCacheService>.Instance)
                .ObtenerAsync("SW201", 1, "Listas y pilas");
        });
        var vectores = await Task.WhenAll(tareas);

        vectores.Should().OnlyContain(v => v != null && v.SequenceEqual(OllamaLento.Vector));
        (await msc.ContarAsync("SELECT COUNT(*) FROM EMBEDDING_UNIDAD")).Should().Be(1, "el índice único decide");
        _salida.WriteLine($"[Caché real] 10 escrituras simultáneas → 1 fila; Ollama llamado {ollama.Llamadas} veces");

        // Una vez guardado, nadie más llama al modelo.
        var antes = ollama.Llamadas;
        await using (var db = Db(msc.CadenaConexion))
        {
            var v = await new EmbeddingCacheService(db, ollama, opciones, NullLogger<EmbeddingCacheService>.Instance)
                .ObtenerAsync("SW201", 1, "Listas y pilas");
            v.Should().Equal(OllamaLento.Vector);
        }

        ollama.Llamadas.Should().Be(antes);
        (await msc.ContarAsync("SELECT COUNT(*) FROM EMBEDDING_UNIDAD WHERE Dimensiones = 768 AND DATALENGTH(Vector) = 3072"))
            .Should().Be(1, "768 dimensiones float32 = 3072 bytes en varbinary");
    }

    /// <summary>Ollama que tarda 100 ms y devuelve un vector de 768 dimensiones (como nomic-embed-text).</summary>
    private sealed class OllamaLento : IOllamaEmbeddingService
    {
        public static readonly float[] Vector = Enumerable.Range(0, 768).Select(i => MathF.Sin(i) / (i + 1)).ToArray();
        private int _llamadas;
        public int Llamadas => _llamadas;

        public async Task<float[]?> GenerarEmbeddingAsync(string texto, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _llamadas);
            await Task.Delay(100, cancellationToken);
            return Vector.ToArray();
        }

        public Task<bool> EstaDisponibleAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
