using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Siha.Certificados;
using Siha.MsC.Api.Options;
using Siha.MsC.Api.Services;

namespace Siha.MsC.Tests;

/// <summary>El agente lee igual un certificado PDF que un registro manual guardado como JSON.</summary>
public sealed class LectorCertificadosAlmacenTests : IDisposable
{
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), $"siha-almacen-{Guid.NewGuid():N}");

    public LectorCertificadosAlmacenTests() => Directory.CreateDirectory(_raiz);

    public void Dispose() => Directory.Delete(_raiz, recursive: true);

    private LectorCertificadosAlmacen Lector() => new(Options.Create(new AlmacenOptions { RutaAlmacenamiento = _raiz }));

    [Fact]
    public async Task Un_registro_manual_en_json_se_lee_como_certificado()
    {
        var certificado = new CertificadoSiha(
            "MANUAL-20261124-ABC123", new DateOnly(2026, 11, 24), "Registro manual (Operador)", "Ana Lucía", "1804567890",
            "INTERNO", "CAU-P-011", "Universidad Técnica de Ambato", "Tecnologías de la Información", "Software",
            new[] { new MateriaCursada("Física", 1, "Abr-Sep 2025", 8.1m, true) },
            new[] { new SolicitudHomologacion(1, new[] { "Física" }, "Física") },
            new[] { new RevisorCertificado("Ing. Paredes", "cparedes@uta.edu.ec"), new RevisorCertificado("Ing. Vaca", "mvaca@uta.edu.ec") });
        await File.WriteAllBytesAsync(Path.Combine(_raiz, "manual.pdf"), JsonSerializer.SerializeToUtf8Bytes(certificado));

        var leido = await Lector().LeerAsync("manual.pdf");

        leido.Should().NotBeNull();
        leido!.NumeroCertificado.Should().Be("MANUAL-20261124-ABC123");
        leido.Solicitudes.Single().MateriaDestino.Should().Be("Física");
        leido.Revisores.Select(r => r.Correo).Should().Equal("cparedes@uta.edu.ec", "mvaca@uta.edu.ec");
    }

    [Theory]
    [InlineData("{\"a\":1}", true)]
    [InlineData("  \n{", true)]
    [InlineData("%PDF-1.4", false)]
    [InlineData("", false)]
    public void Detecta_si_el_archivo_es_json(string contenido, bool esperado)
        => LectorCertificadosAlmacen.EsJson(System.Text.Encoding.UTF8.GetBytes(contenido)).Should().Be(esperado);
}
