extern alias msa;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Siha.Contracts;
using Siha.Integracion.Tests.Soporte;
using Siha.MsB.Api.Contracts.Dtos;
using Xunit.Abstractions;
using SihaDbContextA = msa::Siha.MsA.Api.Data.SihaDbContextA;

namespace Siha.Integracion.Tests;

/// <summary>
/// Recorrido completo con INFRAESTRUCTURA REAL: SQL Server 2022 (migraciones EF Core reales),
/// RabbitMQ 3.13 (MassTransit real), sistema de archivos real y un suscriptor independiente.
/// Nada de EF InMemory, Moq ni arnés en memoria de MassTransit.
/// </summary>
[Collection(ColeccionInfraestructura.Nombre)]
[Trait("Categoria", "InfraestructuraReal")]
public class FlujoDocumentosRealTests
{
    private static readonly TimeSpan EsperaMensaje = TimeSpan.FromSeconds(30);
    private readonly InfraestructuraReal _infra;
    private readonly ITestOutputHelper _salida;

    public FlujoDocumentosRealTests(InfraestructuraReal infra, ITestOutputHelper salida)
    {
        _infra = infra;
        _salida = salida;
    }

    private static object Solicitud() => new
    {
        tipoTramite = TiposTramite.Externo,
        rutaNormativa = RutasNormativas.Movilidad,
        carreraOrigenCodigo = "SW",
        carreraDestinoCodigo = "TI",
        institucionOrigen = "Universidad de prueba",
        asignaturas = new[]
        {
            new { codigoOrigen = "SW101", nombreOrigen = "Programación I", codigoDestino = "TI101", nota = 9.0, fechaAprobacionCertificada = "2025-07-15" }
        }
    };

    private static byte[] Pdf(string marca) =>
        System.Text.Encoding.ASCII.GetBytes($"%PDF-1.7\n1 0 obj << /Type /Catalog >> endobj\n% {marca}\n%%EOF\n");

    private static string Sha256(byte[] contenido) => Convert.ToHexString(SHA256.HashData(contenido)).ToLowerInvariant();

    private static async Task<ExpedienteAceptadoResponse> RegistrarAsync(HttpClient cliente)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/expedientes", Solicitud());
        respuesta.StatusCode.Should().Be(HttpStatusCode.Created, await respuesta.Content.ReadAsStringAsync());
        return (await respuesta.Content.ReadFromJsonAsync<ExpedienteAceptadoResponse>())!;
    }

    private static async Task<HttpResponseMessage> SubirAsync(HttpClient cliente, Guid id, byte[] contenido, string nombre = "plan-analitico.pdf")
    {
        using var formulario = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(contenido);
        archivo.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        formulario.Add(archivo, "archivo", nombre);
        return await cliente.PostAsync($"/api/expedientes/{id}/documentos", formulario);
    }

    private async Task<T> ConsultarAsync<T>(MsBReal ms, string sql)
    {
        await using var conexion = new SqlConnection(ms.CadenaConexion);
        await conexion.OpenAsync();
        await using var comando = new SqlCommand(sql, conexion);
        var valor = await comando.ExecuteScalarAsync();
        _salida.WriteLine($"[SQL Server real] {sql} => {valor}");
        return (T)Convert.ChangeType(valor!, typeof(T));
    }

    [Fact]
    public async Task Las_migraciones_de_A_y_B_se_aplican_en_SQL_Server_real()
    {
        await using var ms = new MsBReal(_infra);
        ms.CreateClient(); // arranca Program.cs: MigrarAsync contra SQL Server real

        (await ConsultarAsync<int>(ms, "SELECT COUNT(*) FROM __EFMigrationsHistory")).Should().Be(3);
        (await ConsultarAsync<int>(ms,
            "SELECT COUNT(*) FROM sys.indexes WHERE name = 'IX_EXPEDIENTE_DOC_Unico_Validado' AND is_unique = 1 AND has_filter = 1"))
            .Should().Be(1);
        (await ConsultarAsync<int>(ms,
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'EXPEDIENTE' AND COLUMN_NAME = 'SelloConcurrencia'"))
            .Should().Be(1);

        // DB A: mismas migraciones que ejecuta el Microservicio A al arrancar en Docker Compose.
        var baseA = $"SihaDbA_it_{Guid.NewGuid():N}"[..30];
        var opcionesA = new DbContextOptionsBuilder<SihaDbContextA>().UseSqlServer(_infra.CadenaConexion(baseA)).Options;
        await using var dbA = new SihaDbContextA(opcionesA);
        await dbA.Database.MigrateAsync();

        var pendientes = await dbA.Database.GetPendingMigrationsAsync();
        var aplicadas = await dbA.Database.GetAppliedMigrationsAsync();
        _salida.WriteLine($"[DB A] migraciones aplicadas: {string.Join(", ", aplicadas)}");
        pendientes.Should().BeEmpty();
        aplicadas.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Recorrido_completo_registro_carga_envio_y_recepcion_en_RabbitMQ()
    {
        await using var espia = await SuscriptorEspia.IniciarAsync(_infra);
        await using var ms = new MsBReal(_infra);
        var cliente = ms.ClienteOperador();
        var pdf = Pdf("recorrido");

        var borrador = await RegistrarAsync(cliente);
        borrador.Estado.Should().Be(ExpedienteEstados.PendienteDocumentos);

        // Ningún mensaje mientras es borrador.
        (await espia.EsperarAsync(borrador.Id, TimeSpan.FromSeconds(2))).Should().BeEmpty();

        var carga = await SubirAsync(cliente, borrador.Id, pdf);
        carga.StatusCode.Should().Be(HttpStatusCode.Created);
        var documento = (await carga.Content.ReadFromJsonAsync<DocumentoResponse>())!;
        documento.HashSha256.Should().Be(Sha256(pdf));

        // Persistencia real del archivo: existe en disco y su contenido es idéntico.
        var rutaFisica = Path.Combine(ms.RutaAlmacenamiento, documento.RutaAlmacenamiento);
        File.Exists(rutaFisica).Should().BeTrue();
        Sha256(await File.ReadAllBytesAsync(rutaFisica)).Should().Be(documento.HashSha256);

        var envio = await cliente.PostAsync($"/api/expedientes/{borrador.Id}/enviar", null);
        envio.StatusCode.Should().Be(HttpStatusCode.Accepted, await envio.Content.ReadAsStringAsync());

        var recibidos = await espia.EsperarAsync(borrador.Id, EsperaMensaje);
        var mensaje = recibidos.Should().ContainSingle().Subject;
        _salida.WriteLine($"[RabbitMQ real] Recibido ExpedienteId={mensaje.Evento.ExpedienteId} CorrelationId(cabecera)={mensaje.CorrelationId} MessageId={mensaje.MessageId}");

        mensaje.CorrelationId.Should().Be(borrador.CorrelationId, "la cabecera CorrelationId debe conservarse");
        mensaje.Evento.CorrelationId.Should().Be(borrador.CorrelationId);
        mensaje.MessageId.Should().Be(borrador.Id, "MessageId determinista = ExpedienteId para deduplicar");
        mensaje.Evento.Version.Should().Be("1.1");
        mensaje.Evento.DocumentoIds.Should().Equal(documento.Id);
        mensaje.Evento.Documentos.Should().ContainSingle(d =>
            d.DocumentoId == documento.Id && d.HashSha256 == Sha256(pdf) && d.TamanoBytes == pdf.Length);

        (await ConsultarAsync<string>(ms, $"SELECT Estado FROM EXPEDIENTE WHERE Id = '{borrador.Id}'"))
            .Should().Be(ExpedienteEstados.PendienteContenidos);
    }

    [Fact]
    public async Task Reintentos_y_cargas_simultaneas_del_mismo_PDF_no_duplican()
    {
        await using var ms = new MsBReal(_infra);
        var cliente = ms.ClienteOperador();
        var pdf = Pdf("duplicado");
        var borrador = await RegistrarAsync(cliente);

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => SubirAsync(cliente, borrador.Id, pdf)));
        var codigos = respuestas.Select(r => r.StatusCode).ToList();
        _salida.WriteLine($"Cargas simultáneas: {string.Join(", ", codigos)}");

        codigos.Should().OnlyContain(c => c == HttpStatusCode.Created || c == HttpStatusCode.OK);
        var ids = new List<Guid>();
        foreach (var respuesta in respuestas)
        {
            ids.Add((await respuesta.Content.ReadFromJsonAsync<DocumentoResponse>())!.Id);
        }

        ids.Distinct().Should().HaveCount(1, "todas las cargas del mismo contenido devuelven el mismo documento");
        (await ConsultarAsync<int>(ms,
            $"SELECT COUNT(*) FROM EXPEDIENTE_DOC WHERE ExpedienteId = '{borrador.Id}' AND EstadoRecepcion = 'VALIDADO'"))
            .Should().Be(1);
        Directory.GetFiles(ms.RutaAlmacenamiento, "*.pdf", SearchOption.AllDirectories).Should().HaveCount(1,
            "los archivos de las cargas perdedoras se eliminan (sin huérfanos)");
    }

    [Fact]
    public async Task Cargas_simultaneas_de_PDF_distintos_se_aceptan_todas()
    {
        await using var ms = new MsBReal(_infra);
        var cliente = ms.ClienteOperador();
        var borrador = await RegistrarAsync(cliente);
        var pdfs = Enumerable.Range(0, 4).Select(i => Pdf($"distinto-{i}")).ToList();

        var respuestas = await Task.WhenAll(pdfs.Select(pdf => SubirAsync(cliente, borrador.Id, pdf)));
        var codigos = respuestas.Select(r => r.StatusCode).ToList();
        _salida.WriteLine($"Cargas simultáneas de PDF distintos: {string.Join(", ", codigos)}");

        codigos.Should().OnlyContain(c => c == HttpStatusCode.Created,
            "el sello de concurrencia no debe rechazar cargas legítimas que llegan a la vez");
        (await ConsultarAsync<int>(ms,
            $"SELECT COUNT(*) FROM EXPEDIENTE_DOC WHERE ExpedienteId = '{borrador.Id}' AND EstadoRecepcion = 'VALIDADO'"))
            .Should().Be(pdfs.Count);
        Directory.GetFiles(ms.RutaAlmacenamiento, "*.pdf", SearchOption.AllDirectories).Should().HaveCount(pdfs.Count);
    }

    [Fact]
    public async Task Envios_simultaneos_publican_un_solo_mensaje()
    {
        await using var espia = await SuscriptorEspia.IniciarAsync(_infra);
        await using var ms = new MsBReal(_infra);
        var cliente = ms.ClienteOperador();
        var borrador = await RegistrarAsync(cliente);
        (await SubirAsync(cliente, borrador.Id, Pdf("concurrencia"))).EnsureSuccessStatusCode();

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => cliente.PostAsync($"/api/expedientes/{borrador.Id}/enviar", null)));
        _salida.WriteLine($"Envíos simultáneos: {string.Join(", ", respuestas.Select(r => (int)r.StatusCode))}");

        respuestas.Count(r => r.StatusCode == HttpStatusCode.Accepted).Should().Be(1);
        respuestas.Should().OnlyContain(r =>
            r.StatusCode == HttpStatusCode.Accepted || r.StatusCode == HttpStatusCode.Conflict || r.StatusCode == HttpStatusCode.OK);

        await espia.EsperarAsync(borrador.Id, EsperaMensaje);
        await Task.Delay(TimeSpan.FromSeconds(3)); // margen para detectar un posible duplicado
        espia.Recibidos.Count(m => m.Evento.ExpedienteId == borrador.Id).Should().Be(1);
        (await ConsultarAsync<int>(ms, $"SELECT IntentosPublicacion FROM EXPEDIENTE WHERE Id = '{borrador.Id}'")).Should().Be(1);
    }

    [Fact]
    public async Task No_se_publica_si_el_PDF_ya_no_existe_en_el_almacenamiento()
    {
        await using var espia = await SuscriptorEspia.IniciarAsync(_infra);
        await using var ms = new MsBReal(_infra);
        var cliente = ms.ClienteOperador();
        var borrador = await RegistrarAsync(cliente);
        var documento = (await (await SubirAsync(cliente, borrador.Id, Pdf("borrado"))).Content.ReadFromJsonAsync<DocumentoResponse>())!;

        File.Delete(Path.Combine(ms.RutaAlmacenamiento, documento.RutaAlmacenamiento));

        var envio = await cliente.PostAsync($"/api/expedientes/{borrador.Id}/enviar", null);
        _salida.WriteLine(await envio.Content.ReadAsStringAsync());

        envio.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await espia.EsperarAsync(borrador.Id, TimeSpan.FromSeconds(3))).Should().BeEmpty();
        (await ConsultarAsync<string>(ms, $"SELECT EstadoRecepcion FROM EXPEDIENTE_DOC WHERE Id = '{documento.Id}'"))
            .Should().Be("NO_DISPONIBLE");
    }

    [Fact]
    public async Task Un_fallo_del_almacenamiento_responde_503_sin_dejar_filas()
    {
        // La raíz configurada es un archivo, no un directorio: cualquier escritura falla con IOException.
        var rutaInvalida = Path.Combine(Path.GetTempPath(), $"siha-no-es-directorio-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(rutaInvalida, "x");

        await using var ms = new MsBReal(_infra, rutaInvalida);
        var cliente = ms.ClienteOperador();
        var borrador = await RegistrarAsync(cliente);

        var carga = await SubirAsync(cliente, borrador.Id, Pdf("disco"));
        _salida.WriteLine(await carga.Content.ReadAsStringAsync());

        carga.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await ConsultarAsync<int>(ms, $"SELECT COUNT(*) FROM EXPEDIENTE_DOC WHERE ExpedienteId = '{borrador.Id}'")).Should().Be(0);
        (await ConsultarAsync<string>(ms, $"SELECT Estado FROM EXPEDIENTE WHERE Id = '{borrador.Id}'"))
            .Should().Be(ExpedienteEstados.PendienteDocumentos);
    }

    [Fact]
    public async Task Caida_y_recuperacion_del_bus_conservan_el_CorrelationId_y_el_mensaje_llega()
    {
        await using var espia = await SuscriptorEspia.IniciarAsync(_infra);
        await using var ms = new MsBReal(_infra);
        var cliente = ms.ClienteOperador();
        var borrador = await RegistrarAsync(cliente);
        (await SubirAsync(cliente, borrador.Id, Pdf("caida-bus"))).EnsureSuccessStatusCode();

        // Caída real del broker: el contenedor RabbitMQ se congela (deja de responder en la red).
        await _infra.RabbitMq.PauseAsync();
        _salida.WriteLine($"{DateTimeOffset.UtcNow:O} RabbitMQ PAUSADO");

        HttpResponseMessage envio;
        try
        {
            envio = await cliente.PostAsync($"/api/expedientes/{borrador.Id}/enviar", null);
        }
        finally
        {
            await _infra.RabbitMq.UnpauseAsync();
            _salida.WriteLine($"{DateTimeOffset.UtcNow:O} RabbitMQ REANUDADO");
        }

        var fallido = (await envio.Content.ReadFromJsonAsync<ExpedienteAceptadoResponse>())!;
        _salida.WriteLine($"Durante la caída: HTTP {(int)envio.StatusCode} Estado={fallido.Estado} Publicado={fallido.Publicado}");
        envio.StatusCode.Should().Be(HttpStatusCode.Accepted);
        fallido.Publicado.Should().BeFalse();
        fallido.Estado.Should().Be(ExpedienteEstados.FalloPublicacion);
        fallido.CorrelationId.Should().Be(borrador.CorrelationId);

        // Recuperación automática: el worker reintenta cada 2 s sin intervención del operador.
        var recibidos = await espia.EsperarAsync(borrador.Id, TimeSpan.FromSeconds(90));
        var mensaje = recibidos.Should().NotBeEmpty("el reintento automático debe publicar tras la recuperación").And.Subject.First();
        _salida.WriteLine($"{mensaje.RecibidoUtc:O} Recibido tras la recuperación. CorrelationId={mensaje.CorrelationId}");

        mensaje.CorrelationId.Should().Be(borrador.CorrelationId);
        mensaje.Evento.CorrelationId.Should().Be(borrador.CorrelationId);

        var estado = "";
        for (var i = 0; i < 20 && estado != ExpedienteEstados.PendienteContenidos; i++)
        {
            estado = await ConsultarAsync<string>(ms, $"SELECT Estado FROM EXPEDIENTE WHERE Id = '{borrador.Id}'");
            await Task.Delay(500);
        }

        estado.Should().Be(ExpedienteEstados.PendienteContenidos);
        (await ConsultarAsync<int>(ms, $"SELECT IntentosPublicacion FROM EXPEDIENTE WHERE Id = '{borrador.Id}'"))
            .Should().BeGreaterThanOrEqualTo(2);
        (await ConsultarAsync<string>(ms, $"SELECT CONVERT(varchar(36), CorrelationId) FROM EXPEDIENTE WHERE Id = '{borrador.Id}'"))
            .Should().BeEquivalentTo(borrador.CorrelationId.ToString());
    }
}
