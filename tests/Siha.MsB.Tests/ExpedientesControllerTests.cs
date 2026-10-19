using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Siha.Contracts;
using Siha.Contracts.Events;
using Siha.MsB.Api.Contracts.Dtos;
using Siha.MsB.Api.Controllers;
using Siha.MsB.Api.Data;
using Siha.MsB.Api.Events;
using Siha.MsB.Tests.Soporte;

namespace Siha.MsB.Tests;

/// <summary>
/// Pruebas del flujo de registro de expedientes (US-05 / US-06, tarea S1-S03): el endpoint debe
/// seguir devolviendo 202 Accepted y dejando el expediente en el estado correcto según el
/// resultado de la publicación en el bus.
/// </summary>
public class ExpedientesControllerTests
{
    private readonly SihaDbContextB _db = Pruebas.CrearContextoB();
    private readonly Mock<IExpedienteEventPublisher> _publicador = new();

    private ExpedientesController CrearControlador() =>
        new(_db, _publicador.Object, NullLogger<ExpedientesController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private void SimularPublicacion(bool ok, string? error = null) =>
        _publicador
            .Setup(p => p.PublicarAsync(It.IsAny<SolicitudCreadaEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ok, error));

    private static CrearExpedienteRequest Solicitud(
        string tipoTramite = TiposTramite.Interno,
        string rutaNormativa = RutasNormativas.CambioCarreraOu) => new(
        tipoTramite,
        rutaNormativa,
        "SW",
        "TI",
        "Universidad Técnica de Ambato",
        new List<DocumentoRequest>
        {
            new("notas.pdf", "2026/10/notas.pdf", null, 1024)
        },
        new List<AsignaturaRequest>
        {
            new("SW101", "Fundamentos de Programación", "TI101", 8.5, new DateOnly(2025, 7, 15)),
            new("SW201", "Estructuras de Datos", "TI201", 9.0, new DateOnly(2025, 7, 15))
        });

    [Fact]
    public async Task Registra_el_expediente_y_devuelve_202_cuando_la_publicacion_funciona()
    {
        SimularPublicacion(ok: true);

        var resultado = await CrearControlador().Registrar(Solicitud(), CancellationToken.None);

        var aceptado = resultado.Result.Should().BeOfType<AcceptedResult>().Subject;
        var respuesta = aceptado.Value.Should().BeOfType<ExpedienteAceptadoResponse>().Subject;

        respuesta.Publicado.Should().BeTrue();
        respuesta.Estado.Should().Be(ExpedienteEstados.PendienteContenidos);

        var expediente = _db.Expedientes.Single(e => e.Id == respuesta.Id);
        expediente.Estado.Should().Be(ExpedienteEstados.PendienteContenidos);
        expediente.EventoPublicado.Should().BeTrue();
        expediente.IntentosPublicacion.Should().Be(1);
        expediente.CorrelationId.Should().Be(respuesta.CorrelationId);
        expediente.FechaPublicacionUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Deja_el_expediente_en_estado_recuperable_cuando_la_publicacion_falla()
    {
        SimularPublicacion(ok: false, error: "Broker RabbitMQ no disponible.");

        var resultado = await CrearControlador().Registrar(Solicitud(), CancellationToken.None);

        // El flujo no se rompe: sigue siendo 202 Accepted (el expediente ya está persistido).
        var aceptado = resultado.Result.Should().BeOfType<AcceptedResult>().Subject;
        var respuesta = aceptado.Value.Should().BeOfType<ExpedienteAceptadoResponse>().Subject;

        respuesta.Publicado.Should().BeFalse();
        respuesta.Estado.Should().Be(ExpedienteEstados.FalloPublicacion);

        var expediente = _db.Expedientes.Single(e => e.Id == respuesta.Id);
        expediente.Estado.Should().Be(ExpedienteEstados.FalloPublicacion);
        expediente.EventoPublicado.Should().BeFalse();
        expediente.UltimoErrorPublicacion.Should().Be("Broker RabbitMQ no disponible.");
        expediente.IntentosPublicacion.Should().Be(1);
    }

    [Fact]
    public async Task El_evento_publicado_cumple_el_contrato_y_conserva_el_correlation_id()
    {
        SolicitudCreadaEvent? capturado = null;
        _publicador
            .Setup(p => p.PublicarAsync(It.IsAny<SolicitudCreadaEvent>(), It.IsAny<CancellationToken>()))
            .Callback<SolicitudCreadaEvent, CancellationToken>((evento, _) => capturado = evento)
            .ReturnsAsync((true, (string?)null));

        var resultado = await CrearControlador().Registrar(Solicitud(rutaNormativa: RutasNormativas.Movilidad), CancellationToken.None);
        var respuesta = resultado.Result.Should().BeOfType<AcceptedResult>().Subject.Value
            .Should().BeOfType<ExpedienteAceptadoResponse>().Subject;

        capturado.Should().NotBeNull();
        capturado!.ExpedienteId.Should().Be(respuesta.Id);
        capturado.CorrelationId.Should().Be(respuesta.CorrelationId);
        capturado.RutaNormativa.Should().Be(RutasNormativas.Movilidad);
        capturado.TipoTramite.Should().Be(TiposTramite.Interno);
        capturado.CarreraOrigenCodigo.Should().Be("SW");
        capturado.CarreraDestinoCodigo.Should().Be("TI");
        capturado.AsignaturasOrigen.Should().BeEquivalentTo(new[] { "SW101", "SW201" });
        capturado.Estado.Should().Be(ExpedienteEstados.PendienteContenidos);
        capturado.Version.Should().Be("1.0");
        capturado.DocumentoIds.Should().HaveCount(1);
    }

    [Fact]
    public async Task Persiste_la_fecha_de_aprobacion_certificada_de_las_materias()
    {
        SimularPublicacion(ok: true);

        var resultado = await CrearControlador().Registrar(Solicitud(), CancellationToken.None);
        var respuesta = resultado.Result.Should().BeOfType<AcceptedResult>().Subject.Value
            .Should().BeOfType<ExpedienteAceptadoResponse>().Subject;

        var asignaturas = _db.ExpedienteAsignaturas.Where(a => a.ExpedienteId == respuesta.Id).ToList();

        asignaturas.Should().HaveCount(2);
        asignaturas.Should().OnlyContain(a => a.FechaAprobacionCertificada == new DateOnly(2025, 7, 15));
    }

    [Theory]
    [InlineData("OTRO", RutasNormativas.CambioCarreraOu)]
    [InlineData(TiposTramite.Interno, "CAU-P-999")]
    public async Task Rechaza_solicitudes_con_tipo_o_ruta_invalidos(string tipoTramite, string rutaNormativa)
    {
        SimularPublicacion(ok: true);

        var resultado = await CrearControlador().Registrar(Solicitud(tipoTramite, rutaNormativa), CancellationToken.None);

        resultado.Result.Should().BeOfType<BadRequestObjectResult>();
        _db.Expedientes.Should().BeEmpty();
    }

    [Fact]
    public async Task Republicar_conserva_el_correlation_id_original()
    {
        SimularPublicacion(ok: false, error: "sin bus");

        var registro = await CrearControlador().Registrar(Solicitud(), CancellationToken.None);
        var aceptada = registro.Result.Should().BeOfType<AcceptedResult>().Subject.Value
            .Should().BeOfType<ExpedienteAceptadoResponse>().Subject;

        var correlationOriginal = aceptada.CorrelationId;

        SimularPublicacion(ok: true);
        var republicacion = await CrearControlador().Republicar(aceptada.Id, CancellationToken.None);

        var respuesta = republicacion.Result.Should().BeOfType<AcceptedResult>().Subject.Value
            .Should().BeOfType<ExpedienteAceptadoResponse>().Subject;

        respuesta.CorrelationId.Should().Be(correlationOriginal);
        respuesta.Estado.Should().Be(ExpedienteEstados.PendienteContenidos);

        var expediente = _db.Expedientes.Single(e => e.Id == aceptada.Id);
        expediente.IntentosPublicacion.Should().Be(2);
        expediente.CorrelationId.Should().Be(correlationOriginal);
    }
}
