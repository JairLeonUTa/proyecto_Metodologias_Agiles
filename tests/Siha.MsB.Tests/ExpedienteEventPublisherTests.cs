using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Siha.Contracts;
using Siha.Contracts.Events;
using Siha.MsB.Api.Events;

namespace Siha.MsB.Tests;

/// <summary>
/// Pruebas de la publicación resiliente del evento (US-06, tarea S1-S03) con
/// <see cref="IPublishEndpoint"/> simulado.
/// </summary>
public class ExpedienteEventPublisherTests
{
    private readonly Mock<IPublishEndpoint> _endpoint = new(MockBehavior.Loose);
    private readonly List<SolicitudCreadaEvent> _publicados = new();

    private ExpedienteEventPublisher CrearPublicador() =>
        new(_endpoint.Object, NullLogger<ExpedienteEventPublisher>.Instance);

    /// <summary>
    /// Arma el simulador indicando tras cuántos intentos fallidos la publicación debe funcionar
    /// (<c>int.MaxValue</c> = falla siempre). El error se propaga como excepción tal como haría
    /// MassTransit cuando el broker no está disponible.
    /// </summary>
    private void SimularPublicacion(int fallosAntesDeExito = 0)
    {
        var intentos = 0;

        Task Ejecutar()
        {
            intentos++;
            return intentos <= fallosAntesDeExito
                ? Task.FromException(new InvalidOperationException("Broker RabbitMQ no disponible."))
                : Task.CompletedTask;
        }

        // La extensión Publish(..., Action<PublishContext<T>>, ...) delega en una de estas dos
        // sobrecargas de la interfaz, según la implementación de MassTransit: se cubren ambas.
        _endpoint
            .Setup(e => e.Publish(
                It.IsAny<SolicitudCreadaEvent>(),
                It.IsAny<IPipe<PublishContext<SolicitudCreadaEvent>>>(),
                It.IsAny<CancellationToken>()))
            .Callback<SolicitudCreadaEvent, IPipe<PublishContext<SolicitudCreadaEvent>>, CancellationToken>(
                (evento, _, _) => _publicados.Add(evento))
            .Returns(() => Ejecutar());

        _endpoint
            .Setup(e => e.Publish(
                It.IsAny<SolicitudCreadaEvent>(),
                It.IsAny<IPipe<PublishContext>>(),
                It.IsAny<CancellationToken>()))
            .Callback<SolicitudCreadaEvent, IPipe<PublishContext>, CancellationToken>(
                (evento, _, _) => _publicados.Add(evento))
            .Returns(() => Ejecutar());
    }

    private static SolicitudCreadaEvent Evento() => new()
    {
        ExpedienteId = Guid.NewGuid(),
        CorrelationId = Guid.NewGuid(),
        CodigoExpediente = "EXP-20261018-ABC123",
        TipoTramite = TiposTramite.Interno,
        RutaNormativa = RutasNormativas.CambioCarreraOu,
        CarreraOrigenCodigo = "SW",
        CarreraDestinoCodigo = "TI",
        DocumentoIds = new[] { Guid.NewGuid() },
        AsignaturasOrigen = new[] { "SW101" }
    };

    [Fact]
    public async Task Publica_exitosamente_en_el_primer_intento()
    {
        SimularPublicacion();
        var evento = Evento();

        var (ok, error) = await CrearPublicador().PublicarAsync(evento);

        ok.Should().BeTrue();
        error.Should().BeNull();
        _publicados.Should().HaveCount(1);
    }

    [Fact]
    public async Task Reintenta_hasta_lograr_la_publicacion()
    {
        SimularPublicacion(fallosAntesDeExito: 2);
        var evento = Evento();

        var (ok, error) = await CrearPublicador().PublicarAsync(evento);

        ok.Should().BeTrue();
        error.Should().BeNull();
        _publicados.Should().HaveCount(ExpedienteEventPublisher.MaxIntentos);
    }

    [Fact]
    public async Task Devuelve_fallo_cuando_se_agotan_los_intentos()
    {
        SimularPublicacion(fallosAntesDeExito: int.MaxValue);
        var evento = Evento();

        var (ok, error) = await CrearPublicador().PublicarAsync(evento);

        ok.Should().BeFalse();
        error.Should().Contain("RabbitMQ");
        _publicados.Should().HaveCount(ExpedienteEventPublisher.MaxIntentos);
    }

    [Fact]
    public async Task Conserva_el_correlation_id_en_todos_los_intentos()
    {
        SimularPublicacion(fallosAntesDeExito: 2);
        var evento = Evento();

        await CrearPublicador().PublicarAsync(evento);

        _publicados.Should().NotBeEmpty();
        _publicados.Should().OnlyContain(e => e.CorrelationId == evento.CorrelationId);
        _publicados.Should().OnlyContain(e => e.ExpedienteId == evento.ExpedienteId);
    }

    [Fact]
    public void La_politica_de_reintentos_es_de_tres_intentos()
    {
        ExpedienteEventPublisher.MaxIntentos.Should().Be(3);
    }
}
