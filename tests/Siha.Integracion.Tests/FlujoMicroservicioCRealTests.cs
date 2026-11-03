using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using FluentAssertions;
using Siha.Contracts;
using Siha.Contracts.Events;
using Siha.Integracion.Tests.Soporte;
using Siha.MsB.Api.Contracts.Dtos;
using Xunit.Abstractions;

namespace Siha.Integracion.Tests;

/// <summary>
/// Sprint 2 con INFRAESTRUCTURA REAL: SQL Server 2022 (DB B y DB C), RabbitMQ 3.13 y el volumen de
/// archivos compartido entre el Microservicio B y el Microservicio C.
///
/// S2-S09: flujo asíncrono B → RabbitMQ → C (expediente → evento → recepción idempotente en C).
/// S2-S02: idempotencia ante entregas duplicadas y simultáneas, que solo la clave primaria real de
///         PROCESSED_EVENTS puede demostrar (EF InMemory no lanza la violación 2627).
/// </summary>
[Collection(ColeccionInfraestructura.Nombre)]
[Trait("Categoria", "InfraestructuraReal")]
public class FlujoMicroservicioCRealTests
{
    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(45);
    private readonly InfraestructuraReal _infra;
    private readonly ITestOutputHelper _salida;

    public FlujoMicroservicioCRealTests(InfraestructuraReal infra, ITestOutputHelper salida)
    {
        _infra = infra;
        _salida = salida;
    }

    private static string NuevoVolumen() =>
        Path.Combine(Path.GetTempPath(), "siha-integracion", Guid.NewGuid().ToString("N"));

    private static byte[] Pdf(string marca) =>
        System.Text.Encoding.ASCII.GetBytes($"%PDF-1.7\n1 0 obj << /Type /Catalog >> endobj\n% {marca}\n%%EOF\n");

    private static string Sha256(byte[] contenido) => Convert.ToHexString(SHA256.HashData(contenido)).ToLowerInvariant();

    /// <summary>Escribe un PDF en el volumen y arma el evento que publicaría el Microservicio B.</summary>
    private static SolicitudCreadaEvent EventoConPdf(string volumen, byte[] pdf, string? hash = null)
    {
        var expedienteId = Guid.NewGuid();
        var documentoId = Guid.NewGuid();
        var relativa = Path.Combine(expedienteId.ToString("N"), $"{documentoId:N}.pdf");
        var fisica = Path.Combine(volumen, relativa);
        Directory.CreateDirectory(Path.GetDirectoryName(fisica)!);
        File.WriteAllBytes(fisica, pdf);

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
            AsignaturasOrigen = new[] { "TI101" },
            Documentos = new[]
            {
                new DocumentoReferencia
                {
                    DocumentoId = documentoId,
                    RutaAlmacenamiento = relativa,
                    HashSha256 = hash ?? Sha256(pdf),
                    TamanoBytes = pdf.Length
                }
            }
        };
    }

    private static string SqlEventos(Guid expedienteId) =>
        $"SELECT COUNT(*) FROM PROCESSED_EVENTS WHERE ExpedienteId = '{expedienteId}'";

    [Fact]
    public async Task Las_migraciones_de_DB_C_se_aplican_en_SQL_Server_real()
    {
        await using var msc = new MsCReal(_infra, NuevoVolumen());
        await msc.IniciarAsync();

        (await msc.ContarAsync("SELECT COUNT(*) FROM __EFMigrationsHistory")).Should().BeGreaterThanOrEqualTo(2);
        foreach (var tabla in new[] { "PROCESSED_EVENTS", "EXTRACTION", "EXTRACTION_UNIT" })
        {
            (await msc.ContarAsync($"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{tabla}'"))
                .Should().Be(1, $"la tabla {tabla} debe existir en DB C");
        }

        _salida.WriteLine($"[DB C real] {msc.BaseDatos}: migraciones aplicadas y tablas creadas.");
    }

    [Fact]
    public async Task Recorrido_B_RabbitMQ_C_el_expediente_enviado_llega_una_vez_con_su_CorrelationId()
    {
        var volumen = NuevoVolumen();
        await using var msc = new MsCReal(_infra, volumen);
        await msc.IniciarAsync();
        await using var msb = new MsBReal(_infra, volumen);
        var cliente = msb.ClienteOperador();

        // 1. Registro en B
        var registro = await cliente.PostAsJsonAsync("/api/expedientes", new
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
        });
        registro.StatusCode.Should().Be(HttpStatusCode.Created, await registro.Content.ReadAsStringAsync());
        var expediente = (await registro.Content.ReadFromJsonAsync<ExpedienteAceptadoResponse>())!;

        // 2. Carga del PDF en el volumen compartido
        var pdf = Pdf("flujo-b-c");
        using var formulario = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(pdf);
        archivo.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        formulario.Add(archivo, "archivo", "plan-analitico.pdf");
        (await cliente.PostAsync($"/api/expedientes/{expediente.Id}/documentos", formulario)).StatusCode
            .Should().Be(HttpStatusCode.Created);

        // 3. Envío: B publica en RabbitMQ y responde 202
        var envio = await cliente.PostAsync($"/api/expedientes/{expediente.Id}/enviar", null);
        envio.StatusCode.Should().Be(HttpStatusCode.Accepted, await envio.Content.ReadAsStringAsync());

        // 4. C lo recibe, verifica el PDF del volumen y registra la idempotencia en DB C
        (await msc.EsperarFilasAsync(SqlEventos(expediente.Id), 1, Espera)).Should().Be(1);
        var correlacion = await msc.ValorAsync<string>(
            $"SELECT CONVERT(varchar(36), CorrelationId) FROM PROCESSED_EVENTS WHERE ExpedienteId = '{expediente.Id}'");
        var messageId = await msc.ValorAsync<string>(
            $"SELECT CONVERT(varchar(36), MessageId) FROM PROCESSED_EVENTS WHERE ExpedienteId = '{expediente.Id}'");

        _salida.WriteLine($"[B → RabbitMQ → C] Expediente={expediente.Id} CorrelationId={correlacion} MessageId={messageId}");
        Guid.Parse(correlacion!).Should().Be(expediente.CorrelationId);
        Guid.Parse(messageId!).Should().Be(expediente.Id, "MessageId determinista = ExpedienteId");

        // El reenvío del mismo expediente no crea otra fila en C.
        await cliente.PostAsync($"/api/expedientes/{expediente.Id}/enviar", null);
        await Task.Delay(TimeSpan.FromSeconds(3));
        (await msc.ContarAsync(SqlEventos(expediente.Id))).Should().Be(1);
    }

    [Fact]
    public async Task Entregas_duplicadas_y_simultaneas_del_mismo_expediente_se_registran_una_sola_vez()
    {
        var volumen = NuevoVolumen();
        await using var msc = new MsCReal(_infra, volumen);
        await msc.IniciarAsync();
        await using var publicador = await PublicadorPrueba.IniciarAsync(_infra);

        var evento = EventoConPdf(volumen, Pdf("duplicado"));

        // Diez entregas del mismo mensaje a la vez: el consumidor las procesa en paralelo y
        // solo la clave primaria de PROCESSED_EVENTS en SQL Server decide cuál gana.
        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => publicador.PublicarAsync(evento)));

        (await msc.EsperarFilasAsync(SqlEventos(evento.ExpedienteId), 1, Espera)).Should().Be(1);
        await Task.Delay(TimeSpan.FromSeconds(5)); // margen para que terminen las entregas restantes
        (await msc.ContarAsync(SqlEventos(evento.ExpedienteId))).Should().Be(1);
        (await msc.ContarAsync("SELECT COUNT(*) FROM PROCESSED_EVENTS")).Should().Be(1);
        _salida.WriteLine($"[Idempotencia real] 10 entregas simultáneas de {evento.ExpedienteId} → 1 fila en PROCESSED_EVENTS");
    }

    [Fact]
    public async Task Expedientes_distintos_publicados_a_la_vez_se_registran_todos()
    {
        var volumen = NuevoVolumen();
        await using var msc = new MsCReal(_infra, volumen);
        await msc.IniciarAsync();
        await using var publicador = await PublicadorPrueba.IniciarAsync(_infra);

        var eventos = Enumerable.Range(0, 5).Select(i => EventoConPdf(volumen, Pdf($"distinto-{i}"))).ToList();
        await Task.WhenAll(eventos.Select(publicador.PublicarAsync));

        (await msc.EsperarFilasAsync("SELECT COUNT(*) FROM PROCESSED_EVENTS", eventos.Count, Espera)).Should().Be(eventos.Count);
    }

    [Fact]
    public async Task Un_PDF_alterado_en_el_volumen_no_se_registra_como_procesado()
    {
        var volumen = NuevoVolumen();
        await using var msc = new MsCReal(_infra, volumen);
        await msc.IniciarAsync();
        await using var publicador = await PublicadorPrueba.IniciarAsync(_infra);

        // El hash del evento no coincide con el archivo: C no debe dar por recibido el expediente.
        var alterado = EventoConPdf(volumen, Pdf("alterado"), hash: new string('0', 64));
        var valido = EventoConPdf(volumen, Pdf("valido"));
        await publicador.PublicarAsync(alterado);
        await publicador.PublicarAsync(valido);

        // El válido sirve de testigo de que el consumidor estaba procesando.
        (await msc.EsperarFilasAsync(SqlEventos(valido.ExpedienteId), 1, Espera)).Should().Be(1);
        await Task.Delay(TimeSpan.FromSeconds(5));
        (await msc.ContarAsync(SqlEventos(alterado.ExpedienteId))).Should().Be(0);
    }
}
