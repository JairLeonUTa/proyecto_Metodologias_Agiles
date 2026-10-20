using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Siha.Contracts;
using Siha.Contracts.Events;
using Siha.MsB.Api.Contracts.Dtos;
using Siha.MsB.Api.Controllers;
using Siha.MsB.Api.Data;
using Siha.MsB.Api.Domain;
using Siha.MsB.Api.Events;
using Siha.MsB.Api.Options;
using Siha.MsB.Api.Services;
using Siha.MsB.Tests.Soporte;

namespace Siha.MsB.Tests;

/// <summary>
/// Pruebas SIMULADAS (EF Core InMemory + disco temporal + publicador simulado con Moq) del flujo
/// borrador → carga de PDF en el servidor → envío verificado → publicación.
/// Las mismas reglas se comprueban contra SQL Server y RabbitMQ reales en Siha.Integracion.Tests.
/// </summary>
public class ExpedientesControllerTests : IDisposable
{
    private readonly string _directorio = Pruebas.DirectorioTemporal();
    private readonly string _nombreBaseDatos = Guid.NewGuid().ToString("N");
    private readonly SihaDbContextB _db;
    private readonly Mock<IExpedienteEventPublisher> _publicador = new();
    private readonly List<SolicitudCreadaEvent> _publicados = new();

    public ExpedientesControllerTests()
    {
        _db = Pruebas.CrearContextoB(_nombreBaseDatos);
        SimularPublicacion(ok: true);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directorio))
        {
            Directory.Delete(_directorio, recursive: true);
        }
    }

    private ArchivoStorageService Almacenamiento() =>
        Pruebas.CrearAlmacenamiento(Pruebas.OpcionesArchivos(_directorio));

    private ExpedienteEnvioService Envio(SihaDbContextB? db = null) => new(
        db ?? _db,
        Almacenamiento(),
        _publicador.Object,
        Options.Create(Pruebas.OpcionesArchivos(_directorio)),
        Options.Create(new RepublicacionOptions()),
        NullLogger<ExpedienteEnvioService>.Instance);

    private ExpedientesController CrearControlador() =>
        new(_db, Envio(), NullLogger<ExpedientesController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private DocumentosController CrearControladorDocumentos() =>
        new(_db, Almacenamiento(), NullLogger<DocumentosController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private void SimularPublicacion(bool ok, string? error = null)
    {
        _publicador.Reset();
        _publicador
            .Setup(p => p.PublicarAsync(It.IsAny<SolicitudCreadaEvent>(), It.IsAny<CancellationToken>()))
            .Callback<SolicitudCreadaEvent, CancellationToken>((evento, _) => _publicados.Add(evento))
            .ReturnsAsync((ok, error));
    }

    private static CrearExpedienteRequest Solicitud(
        string tipoTramite = TiposTramite.Interno,
        string rutaNormativa = RutasNormativas.CambioCarreraOu,
        IReadOnlyCollection<DocumentoRequest>? documentos = null) => new(
        tipoTramite,
        rutaNormativa,
        "SW",
        "TI",
        "Universidad Técnica de Ambato",
        documentos,
        new List<AsignaturaRequest>
        {
            new("SW101", "Fundamentos de Programación", "TI101", 8.5, new DateOnly(2025, 7, 15)),
            new("SW201", "Estructuras de Datos", "TI201", 9.0, new DateOnly(2025, 7, 15))
        });

    private async Task<ExpedienteAceptadoResponse> RegistrarBorradorAsync(string rutaNormativa = RutasNormativas.CambioCarreraOu)
    {
        var resultado = await CrearControlador().Registrar(Solicitud(rutaNormativa: rutaNormativa), CancellationToken.None);
        return resultado.Result.Should().BeOfType<CreatedAtActionResult>().Subject.Value
            .Should().BeOfType<ExpedienteAceptadoResponse>().Subject;
    }

    private async Task<DocumentoResponse> CargarPdfAsync(Guid expedienteId, string cuerpo = "contenido")
    {
        var resultado = await CrearControladorDocumentos().Subir(
            expedienteId, Pruebas.Archivo(Pruebas.BytesPdf(cuerpo)), CancellationToken.None);
        return resultado.Result.Should().BeOfType<CreatedAtActionResult>().Subject.Value
            .Should().BeOfType<DocumentoResponse>().Subject;
    }

    [Fact]
    public async Task El_registro_crea_un_borrador_y_no_publica_nada()
    {
        var respuesta = await RegistrarBorradorAsync();

        respuesta.Publicado.Should().BeFalse();
        respuesta.Estado.Should().Be(ExpedienteEstados.PendienteDocumentos);
        _publicados.Should().BeEmpty();

        var expediente = _db.Expedientes.Single(e => e.Id == respuesta.Id);
        expediente.Estado.Should().Be(ExpedienteEstados.PendienteDocumentos);
        expediente.EventoPublicado.Should().BeFalse();
        expediente.IntentosPublicacion.Should().Be(0);
    }

    [Fact]
    public async Task Rechaza_documentos_declarados_por_el_cliente_en_el_registro()
    {
        var falsos = new List<DocumentoRequest> { new("notas.pdf", "../../etc/passwd", "abc", 1024) };

        var resultado = await CrearControlador().Registrar(Solicitud(documentos: falsos), CancellationToken.None);

        resultado.Result.Should().BeOfType<BadRequestObjectResult>();
        _db.Expedientes.Should().BeEmpty();
        _db.ExpedienteDocumentos.Should().BeEmpty();
    }

    [Fact]
    public async Task Enviar_sin_documentos_validados_devuelve_422_y_no_publica()
    {
        var borrador = await RegistrarBorradorAsync();

        var resultado = await CrearControlador().Enviar(borrador.Id, CancellationToken.None);

        resultado.Result.Should().BeOfType<UnprocessableEntityObjectResult>();
        _publicados.Should().BeEmpty();
        _db.Expedientes.Single().Estado.Should().Be(ExpedienteEstados.PendienteDocumentos);
    }

    [Fact]
    public async Task Enviar_con_un_pdf_verificado_publica_el_evento_con_referencias_del_servidor()
    {
        var borrador = await RegistrarBorradorAsync(RutasNormativas.Movilidad);
        var documento = await CargarPdfAsync(borrador.Id);

        var resultado = await CrearControlador().Enviar(borrador.Id, CancellationToken.None);

        var respuesta = resultado.Result.Should().BeOfType<AcceptedResult>().Subject.Value
            .Should().BeOfType<ExpedienteAceptadoResponse>().Subject;
        respuesta.Publicado.Should().BeTrue();
        respuesta.Estado.Should().Be(ExpedienteEstados.PendienteContenidos);

        var evento = _publicados.Should().ContainSingle().Subject;
        evento.ExpedienteId.Should().Be(borrador.Id);
        evento.CorrelationId.Should().Be(borrador.CorrelationId);
        evento.RutaNormativa.Should().Be(RutasNormativas.Movilidad);
        evento.AsignaturasOrigen.Should().BeEquivalentTo(new[] { "SW101", "SW201" });
        evento.Version.Should().Be("1.1");
        evento.DocumentoIds.Should().Equal(documento.Id);

        var referencia = evento.Documentos.Should().ContainSingle().Subject;
        referencia.DocumentoId.Should().Be(documento.Id);
        referencia.HashSha256.Should().Be(documento.HashSha256);
        referencia.RutaAlmacenamiento.Should().Be(documento.RutaAlmacenamiento);
        referencia.TamanoBytes.Should().Be(documento.TamanoBytes);

        var expediente = _db.Expedientes.Single();
        expediente.EventoPublicado.Should().BeTrue();
        expediente.IntentosPublicacion.Should().Be(1);
    }

    [Fact]
    public async Task No_publica_si_el_archivo_desaparecio_del_almacenamiento()
    {
        var borrador = await RegistrarBorradorAsync();
        var documento = await CargarPdfAsync(borrador.Id);
        File.Delete(Path.Combine(_directorio, documento.RutaAlmacenamiento));

        var resultado = await CrearControlador().Enviar(borrador.Id, CancellationToken.None);

        resultado.Result.Should().BeOfType<UnprocessableEntityObjectResult>();
        _publicados.Should().BeEmpty();
        _db.ExpedienteDocumentos.Single().EstadoRecepcion.Should().Be(EstadosRecepcionDocumento.NoDisponible);
    }

    [Fact]
    public async Task No_publica_si_el_contenido_en_disco_fue_alterado()
    {
        var borrador = await RegistrarBorradorAsync();
        var documento = await CargarPdfAsync(borrador.Id, cuerpo: "AAAA");
        var ruta = Path.Combine(_directorio, documento.RutaAlmacenamiento);

        // Mismo tamaño, distinto contenido: solo el SHA-256 recalculado lo detecta.
        await File.WriteAllBytesAsync(ruta, Pruebas.BytesPdf("BBBB"));

        var resultado = await CrearControlador().Enviar(borrador.Id, CancellationToken.None);

        resultado.Result.Should().BeOfType<UnprocessableEntityObjectResult>();
        _publicados.Should().BeEmpty();
        var registrado = _db.ExpedienteDocumentos.Single();
        registrado.EstadoRecepcion.Should().Be(EstadosRecepcionDocumento.NoDisponible);
        registrado.ObservacionValidacion.Should().Contain("SHA-256");
    }

    [Fact]
    public async Task Un_segundo_envio_no_vuelve_a_publicar()
    {
        var borrador = await RegistrarBorradorAsync();
        await CargarPdfAsync(borrador.Id);

        await CrearControlador().Enviar(borrador.Id, CancellationToken.None);
        var segundo = await CrearControlador().Enviar(borrador.Id, CancellationToken.None);
        var republicar = await CrearControlador().Republicar(borrador.Id, CancellationToken.None);

        segundo.Result.Should().BeOfType<OkObjectResult>();
        republicar.Result.Should().BeOfType<OkObjectResult>();
        _publicados.Should().HaveCount(1);
        _db.Expedientes.Single().IntentosPublicacion.Should().Be(1);
    }

    [Fact]
    public async Task Envio_concurrente_desde_otro_contexto_no_publica_dos_veces()
    {
        var borrador = await RegistrarBorradorAsync();
        await CargarPdfAsync(borrador.Id);

        // Dos peticiones con su propio DbContext leen el mismo borrador; la segunda debe chocar con
        // el sello de concurrencia que renovó la primera al reservar el envío.
        using var db1 = Pruebas.CrearContextoB(_nombreBaseDatos);
        using var db2 = Pruebas.CrearContextoB(_nombreBaseDatos);
        var envio1 = Envio(db1);
        var envio2 = Envio(db2);

        var precargado = await db2.Expedientes.FindAsync(borrador.Id);
        precargado.Should().NotBeNull();

        var primero = await envio1.EnviarAsync(borrador.Id, ModoEnvio.Envio);

        // db2 ya tenía la entidad rastreada con el sello antiguo.
        var segundo = await envio2.EnviarAsync(borrador.Id, ModoEnvio.Envio);

        primero.Tipo.Should().Be(TipoResultadoEnvio.Publicado);
        segundo.Tipo.Should().BeOneOf(TipoResultadoEnvio.Conflicto, TipoResultadoEnvio.YaPublicado);
        _publicados.Should().HaveCount(1);
    }

    [Fact]
    public async Task Fallo_del_bus_deja_estado_recuperable_y_republicar_conserva_el_correlation_id()
    {
        var borrador = await RegistrarBorradorAsync();
        await CargarPdfAsync(borrador.Id);
        SimularPublicacion(ok: false, error: "Broker RabbitMQ no disponible.");

        var envio = await CrearControlador().Enviar(borrador.Id, CancellationToken.None);
        var fallido = envio.Result.Should().BeOfType<AcceptedResult>().Subject.Value
            .Should().BeOfType<ExpedienteAceptadoResponse>().Subject;

        fallido.Publicado.Should().BeFalse();
        fallido.Estado.Should().Be(ExpedienteEstados.FalloPublicacion);
        _db.Expedientes.Single().UltimoErrorPublicacion.Should().Be("Broker RabbitMQ no disponible.");

        SimularPublicacion(ok: true);
        var republicacion = await CrearControlador().Republicar(borrador.Id, CancellationToken.None);

        var respuesta = republicacion.Result.Should().BeOfType<AcceptedResult>().Subject.Value
            .Should().BeOfType<ExpedienteAceptadoResponse>().Subject;
        respuesta.CorrelationId.Should().Be(borrador.CorrelationId);
        respuesta.Estado.Should().Be(ExpedienteEstados.PendienteContenidos);
        _publicados.Last().CorrelationId.Should().Be(borrador.CorrelationId);

        var expediente = _db.Expedientes.Single();
        expediente.IntentosPublicacion.Should().Be(2);
        expediente.CorrelationId.Should().Be(borrador.CorrelationId);
    }

    [Fact]
    public async Task Republicar_un_borrador_no_enviado_devuelve_409()
    {
        var borrador = await RegistrarBorradorAsync();
        await CargarPdfAsync(borrador.Id);

        var resultado = await CrearControlador().Republicar(borrador.Id, CancellationToken.None);

        resultado.Result.Should().BeOfType<ConflictObjectResult>();
        _publicados.Should().BeEmpty();
    }

    [Fact]
    public async Task El_reintento_automatico_recupera_los_expedientes_pendientes_de_publicacion()
    {
        var borrador = await RegistrarBorradorAsync();
        await CargarPdfAsync(borrador.Id);
        SimularPublicacion(ok: false, error: "sin bus");
        await CrearControlador().Enviar(borrador.Id, CancellationToken.None);

        SimularPublicacion(ok: true);
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddScoped(_ => Pruebas.CrearContextoB(_nombreBaseDatos));
        servicios.AddScoped(sp => Envio(sp.GetRequiredService<SihaDbContextB>()));
        await using var proveedor = servicios.BuildServiceProvider();

        var worker = new RepublicacionAutomaticaWorker(
            proveedor.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new RepublicacionOptions()),
            NullLogger<RepublicacionAutomaticaWorker>.Instance);

        var publicados = await worker.EjecutarBarridoAsync(CancellationToken.None);

        publicados.Should().Be(1);
        _publicados.Last().CorrelationId.Should().Be(borrador.CorrelationId);
        using var verificacion = Pruebas.CrearContextoB(_nombreBaseDatos);
        verificacion.Expedientes.Single().Estado.Should().Be(ExpedienteEstados.PendienteContenidos);
    }

    [Fact]
    public async Task Persiste_la_fecha_de_aprobacion_certificada_de_las_materias()
    {
        var respuesta = await RegistrarBorradorAsync();

        var asignaturas = _db.ExpedienteAsignaturas.Where(a => a.ExpedienteId == respuesta.Id).ToList();

        asignaturas.Should().HaveCount(2);
        asignaturas.Should().OnlyContain(a => a.FechaAprobacionCertificada == new DateOnly(2025, 7, 15));
    }

    [Theory]
    [InlineData("OTRO", RutasNormativas.CambioCarreraOu)]
    [InlineData(TiposTramite.Interno, "CAU-P-999")]
    public async Task Rechaza_solicitudes_con_tipo_o_ruta_invalidos(string tipoTramite, string rutaNormativa)
    {
        var resultado = await CrearControlador().Registrar(Solicitud(tipoTramite, rutaNormativa), CancellationToken.None);

        resultado.Result.Should().BeOfType<BadRequestObjectResult>();
        _db.Expedientes.Should().BeEmpty();
    }
}
