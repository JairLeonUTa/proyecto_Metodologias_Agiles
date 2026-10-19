using System.Text.Json;
using FluentAssertions;
using Siha.Contracts;
using Siha.Contracts.Events;

namespace Siha.MsB.Tests;

/// <summary>
/// Verificación del contrato compartido <see cref="SolicitudCreadaEvent"/> (US-06, CA1 y CA3,
/// tareas S1-S07 / S1-S09). El evento NO debe transportar binarios: solo referencias.
/// </summary>
public class SolicitudCreadaEventContractTests
{
    private static readonly string[] CamposEsperados =
    {
        "ExpedienteId",
        "CorrelationId",
        "CodigoExpediente",
        "TipoTramite",
        "RutaNormativa",
        "CarreraOrigenCodigo",
        "CarreraDestinoCodigo",
        "DocumentoIds",
        "Documentos",
        "AsignaturasOrigen",
        "Estado",
        "FechaCreacionUtc",
        "Version"
    };

    private static SolicitudCreadaEvent Evento() => new()
    {
        ExpedienteId = Guid.NewGuid(),
        CorrelationId = Guid.NewGuid(),
        CodigoExpediente = "EXP-20261018-ABC123",
        TipoTramite = TiposTramite.Externo,
        RutaNormativa = RutasNormativas.Movilidad,
        CarreraOrigenCodigo = "SW",
        CarreraDestinoCodigo = "TI",
        DocumentoIds = new[] { Guid.NewGuid(), Guid.NewGuid() },
        AsignaturasOrigen = new[] { "SW101", "SW201" }
    };

    [Fact]
    public void Expone_todos_los_campos_del_contrato()
    {
        var propiedades = typeof(SolicitudCreadaEvent)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        propiedades.Should().BeEquivalentTo(CamposEsperados);
    }

    [Fact]
    public void El_contrato_es_un_record_inmutable()
    {
        var tipo = typeof(SolicitudCreadaEvent);

        tipo.IsSealed.Should().BeTrue();
        tipo.GetMethod("<Clone>$").Should().NotBeNull("los record exponen un clon inmutable");
    }

    [Fact]
    public void No_transporta_binarios_solo_referencias()
    {
        var tiposProhibidos = new[]
        {
            typeof(byte[]),
            typeof(Stream),
            typeof(Memory<byte>),
            typeof(ReadOnlyMemory<byte>)
        };

        foreach (var propiedad in typeof(SolicitudCreadaEvent).GetProperties())
        {
            tiposProhibidos.Should().NotContain(propiedad.PropertyType,
                $"el contrato no puede adjuntar binarios (propiedad {propiedad.Name})");

            // Tampoco colecciones de bytes (los documentos viajan como Guid de referencia).
            if (propiedad.PropertyType.IsGenericType)
            {
                propiedad.PropertyType.GetGenericArguments().Should().NotContain(typeof(byte));
            }
        }

        var evento = Evento();
        evento.DocumentoIds.Should().OnlyContain(id => id != Guid.Empty);
    }

    [Fact]
    public void Los_valores_por_defecto_respetan_el_contrato()
    {
        var evento = new SolicitudCreadaEvent();

        evento.Estado.Should().Be(ExpedienteEstados.PendienteContenidos);
        evento.Version.Should().Be("1.1");
        evento.CodigoExpediente.Should().BeEmpty();
        evento.DocumentoIds.Should().BeEmpty();
        evento.AsignaturasOrigen.Should().BeEmpty();
    }

    [Fact]
    public void Se_serializa_y_deserializa_conservando_el_correlation_id()
    {
        var original = Evento();

        var json = JsonSerializer.Serialize(original);
        json.Should().NotContain("%PDF");

        var recuperado = JsonSerializer.Deserialize<SolicitudCreadaEvent>(json);

        recuperado.Should().NotBeNull();
        recuperado!.CorrelationId.Should().Be(original.CorrelationId);
        recuperado.ExpedienteId.Should().Be(original.ExpedienteId);
        recuperado.DocumentoIds.Should().BeEquivalentTo(original.DocumentoIds);
        recuperado.AsignaturasOrigen.Should().BeEquivalentTo(original.AsignaturasOrigen);
        recuperado.RutaNormativa.Should().Be(RutasNormativas.Movilidad);
        recuperado.Version.Should().Be("1.1");
    }
}
