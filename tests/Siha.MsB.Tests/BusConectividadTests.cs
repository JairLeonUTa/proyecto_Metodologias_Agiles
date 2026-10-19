using System.Collections.Concurrent;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Siha.Contracts;
using Siha.Contracts.Events;
using Siha.MsB.Api.Consumers;
using Siha.MsB.Api.Events;

namespace Siha.MsB.Tests;

/// <summary>
/// Prueba de integración de la conectividad del bus (US-06, tareas S1-S07 / S1-S09).
///
/// Usa el arnés de pruebas oficial de MassTransit sobre el transporte en memoria, de modo que
/// valida de extremo a extremo la publicación real de <see cref="SolicitudCreadaEvent"/> por
/// <see cref="ExpedienteEventPublisher"/> y su consumo por el suscriptor del Microservicio B,
/// sin depender de que RabbitMQ esté levantado en el entorno de pruebas.
/// </summary>
public class BusConectividadTests
{
    /// <summary>Consumidor espía que conserva los mensajes recibidos para inspeccionarlos.</summary>
    private sealed class ConsumidorEspia : IConsumer<SolicitudCreadaEvent>
    {
        public static readonly ConcurrentQueue<SolicitudCreadaEvent> Recibidos = new();

        public Task Consume(ConsumeContext<SolicitudCreadaEvent> context)
        {
            Recibidos.Enqueue(context.Message);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task El_evento_publicado_llega_al_bus_con_su_correlation_id_y_sin_binarios()
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddMassTransitTestHarness(configurador =>
        {
            configurador.AddConsumer<VerificacionSolicitudConsumer>();
            configurador.AddConsumer<ConsumidorEspia>();
        });

        await using var proveedor = servicios.BuildServiceProvider();
        var arnes = proveedor.GetRequiredService<ITestHarness>();
        arnes.TestTimeout = TimeSpan.FromSeconds(15);

        await arnes.Start();

        try
        {
            var publicador = new ExpedienteEventPublisher(
                proveedor.GetRequiredService<IPublishEndpoint>(),
                NullLogger<ExpedienteEventPublisher>.Instance);

            var evento = new SolicitudCreadaEvent
            {
                ExpedienteId = Guid.NewGuid(),
                CorrelationId = Guid.NewGuid(),
                CodigoExpediente = "EXP-20261018-BUS001",
                TipoTramite = TiposTramite.Interno,
                RutaNormativa = RutasNormativas.CambioCarreraOu,
                CarreraOrigenCodigo = "SW",
                CarreraDestinoCodigo = "TI",
                DocumentoIds = new[] { Guid.NewGuid() },
                AsignaturasOrigen = new[] { "SW101" }
            };

            var (ok, error) = await publicador.PublicarAsync(evento);

            ok.Should().BeTrue();
            error.Should().BeNull();

            // 1) El suscriptor real del Microservicio B (y el espía) reciben el mensaje.
            (await arnes.Consumed.Any<SolicitudCreadaEvent>()).Should().BeTrue();
            (await arnes.Published.Any<SolicitudCreadaEvent>()).Should().BeTrue();

            // 2) El contenido llega intacto, conservando el CorrelationId.
            var recibido = await EsperarMensajeAsync();
            recibido.Should().NotBeNull();
            recibido!.CorrelationId.Should().Be(evento.CorrelationId);
            recibido.ExpedienteId.Should().Be(evento.ExpedienteId);
            recibido.CodigoExpediente.Should().Be(evento.CodigoExpediente);
            recibido.RutaNormativa.Should().Be(RutasNormativas.CambioCarreraOu);
            recibido.AsignaturasOrigen.Should().BeEquivalentTo(evento.AsignaturasOrigen);
            recibido.DocumentoIds.Should().BeEquivalentTo(evento.DocumentoIds);
        }
        finally
        {
            await arnes.Stop();
        }
    }

    private static async Task<SolicitudCreadaEvent?> EsperarMensajeAsync()
    {
        var limite = DateTime.UtcNow.AddSeconds(10);

        while (DateTime.UtcNow < limite)
        {
            if (ConsumidorEspia.Recibidos.TryDequeue(out var mensaje))
            {
                return mensaje;
            }

            await Task.Delay(50);
        }

        return null;
    }
}
