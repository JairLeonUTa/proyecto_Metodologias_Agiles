using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Siha.Contracts.Events;
using Siha.MsC.Api.Data;
using Siha.MsC.Api.Domain;
using Siha.MsC.Api.Options;
using Siha.MsC.Api.Services;

namespace Siha.MsC.Tests;

public class ProcesarSolicitudCreadaServiceTests
{
    [Fact]
    public async Task Procesa_evento_valido_y_registra_idempotencia()
    {
        using var temp = new Soporte.DirectorioTemporal();
        var contenido = "contenido pdf de prueba"u8.ToArray();
        temp.CrearArchivo("a.pdf", contenido);
        var evento = CrearEvento(contenido);
        await using var db = CrearDb();
        var servicio = CrearServicio(db, temp.Ruta);

        var resultado = await servicio.ProcesarAsync(evento, evento.ExpedienteId);

        resultado.Should().Be(ResultadoProcesamiento.Procesado);
        (await db.EventosProcesados.CountAsync()).Should().Be(1);
        var registro = await db.EventosProcesados.SingleAsync();
        registro.ExpedienteId.Should().Be(evento.ExpedienteId);
        registro.MessageId.Should().Be(evento.ExpedienteId);
        registro.CorrelationId.Should().Be(evento.CorrelationId);
    }

    [Fact]
    public async Task Ignora_evento_duplicado_sin_crear_otro_registro()
    {
        using var temp = new Soporte.DirectorioTemporal();
        var contenido = "contenido pdf de prueba"u8.ToArray();
        temp.CrearArchivo("a.pdf", contenido);
        var evento = CrearEvento(contenido);
        await using var db = CrearDb();
        var servicio = CrearServicio(db, temp.Ruta);

        var primero = await servicio.ProcesarAsync(evento, evento.ExpedienteId);
        var segundo = await servicio.ProcesarAsync(evento, evento.ExpedienteId);

        primero.Should().Be(ResultadoProcesamiento.Procesado);
        segundo.Should().Be(ResultadoProcesamiento.Duplicado);
        (await db.EventosProcesados.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Pdf_sin_capa_de_texto_cancela_la_extraccion_sin_inventar_datos()
    {
        using var temp = new Soporte.DirectorioTemporal();
        var contenido = "%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\n%%EOF\n"u8.ToArray();
        temp.CrearArchivo("a.pdf", contenido);
        var evento = CrearEvento(contenido);
        await using var db = CrearDb();
        var servicio = CrearServicio(db, temp.Ruta);

        var resultado = await servicio.ProcesarAsync(evento, evento.ExpedienteId);

        resultado.Should().Be(ResultadoProcesamiento.Procesado);
        var extraccion = await db.Extracciones.Include(e => e.Unidades).SingleAsync();
        extraccion.Estado.Should().Be(EstadosExtraccion.Cancelada);
        extraccion.Creditos.Should().BeNull();
        extraccion.Horas.Should().BeNull();
        extraccion.Unidades.Should().BeEmpty();
        extraccion.Observacion.Should().Contain("OCR");
    }

    [Fact]
    public async Task Rechaza_hash_alterado_y_no_registra_el_evento()
    {
        using var temp = new Soporte.DirectorioTemporal();
        var contenido = "contenido pdf de prueba"u8.ToArray();
        temp.CrearArchivo("a.pdf", contenido);
        var evento = CrearEvento(contenido, hash: new string('0', 64));
        await using var db = CrearDb();
        var servicio = CrearServicio(db, temp.Ruta);

        var accion = () => servicio.ProcesarAsync(evento, evento.ExpedienteId);

        await accion.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*SHA-256*");
        (await db.EventosProcesados.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Rechaza_evento_sin_documentos()
    {
        await using var db = CrearDb();
        var servicio = CrearServicio(db, Path.GetTempPath());
        var evento = CrearEvento(Array.Empty<byte>(), incluirDocumento: false);

        var accion = () => servicio.ProcesarAsync(evento, evento.ExpedienteId);

        await accion.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*referencias de documentos*");
    }

    [Fact]
    public async Task Rechaza_ruta_que_sale_del_volumen_compartido()
    {
        using var temp = new Soporte.DirectorioTemporal();
        var almacen = CrearAlmacen(temp.Ruta);
        var referencia = new DocumentoReferencia
        {
            DocumentoId = Guid.NewGuid(),
            RutaAlmacenamiento = "../fuera.pdf",
            HashSha256 = new string('a', 64),
            TamanoBytes = 10
        };

        var verificacion = await almacen.VerificarAsync(referencia);

        verificacion.Disponible.Should().BeFalse();
        verificacion.Motivo.Should().Contain("salir del volumen");
    }

    [Fact]
    public async Task Rechaza_ruta_absoluta()
    {
        using var temp = new Soporte.DirectorioTemporal();
        var almacen = CrearAlmacen(temp.Ruta);
        var referencia = new DocumentoReferencia
        {
            DocumentoId = Guid.NewGuid(),
            RutaAlmacenamiento = Path.Combine(temp.Ruta, "a.pdf"),
            HashSha256 = new string('a', 64),
            TamanoBytes = 10
        };

        var verificacion = await almacen.VerificarAsync(referencia);

        verificacion.Disponible.Should().BeFalse();
        verificacion.Motivo.Should().Contain("relativa");
    }

    [Fact]
    public async Task Rechaza_tamano_alterado()
    {
        using var temp = new Soporte.DirectorioTemporal();
        var contenido = "contenido pdf de prueba"u8.ToArray();
        temp.CrearArchivo("a.pdf", contenido);
        var almacen = CrearAlmacen(temp.Ruta);
        var referencia = new DocumentoReferencia
        {
            DocumentoId = Guid.NewGuid(),
            RutaAlmacenamiento = "a.pdf",
            HashSha256 = Hash(contenido),
            TamanoBytes = contenido.Length + 1
        };

        var verificacion = await almacen.VerificarAsync(referencia);

        verificacion.Disponible.Should().BeFalse();
        verificacion.Motivo.Should().Contain("tamaño");
    }

    private static SihaDbContextC CrearDb()
        => new(new DbContextOptionsBuilder<SihaDbContextC>()
            .UseInMemoryDatabase($"siha-msc-{Guid.NewGuid():N}")
            .Options);

    private static ProcesarSolicitudCreadaService CrearServicio(SihaDbContextC db, string raiz)
        => new(db, CrearAlmacen(raiz),
            new ExtractorPdfService(Options.Create(new AlmacenOptions { RutaAlmacenamiento = raiz }), NullLogger<ExtractorPdfService>.Instance),
            NullLogger<ProcesarSolicitudCreadaService>.Instance);

    private static AlmacenDocumentos CrearAlmacen(string raiz)
        => new(Options.Create(new AlmacenOptions { RutaAlmacenamiento = raiz }));

    private static SolicitudCreadaEvent CrearEvento(
        byte[] contenido,
        string? hash = null,
        bool incluirDocumento = true)
    {
        var evento = new SolicitudCreadaEvent
        {
            ExpedienteId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            CodigoExpediente = "EXT-001",
            TipoTramite = "EXTERNO",
            RutaNormativa = "CAU-P-011",
            CarreraOrigenCodigo = "TI",
            CarreraDestinoCodigo = "SOFT",
            DocumentoIds = Array.Empty<Guid>(),
            Documentos = Array.Empty<DocumentoReferencia>()
        };

        if (!incluirDocumento)
            return evento;

        var documento = new DocumentoReferencia
        {
            DocumentoId = Guid.NewGuid(),
            RutaAlmacenamiento = "a.pdf",
            HashSha256 = hash ?? Hash(contenido),
            TamanoBytes = contenido.Length
        };

        return evento with
        {
            DocumentoIds = new[] { documento.DocumentoId },
            Documentos = new[] { documento }
        };
    }

    private static string Hash(byte[] contenido)
        => Convert.ToHexString(SHA256.HashData(contenido)).ToLowerInvariant();
}
