extern alias msc;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Siha.Contracts;
using Siha.Integracion.Tests.Soporte;
using Xunit.Abstractions;
using ClienteGeneradoMsC = msc::Siha.MsA.Api.Grpc.ReglasGrpc.ReglasGrpcClient;
using ClienteReglasMsC = msc::Siha.MsC.Api.Grpc.ReglasGrpcClient;
using OpcionesReglasMsC = msc::Siha.MsC.Api.Options.ReglasGrpcOptions;
using ReglasGrpcExceptionMsC = msc::Siha.MsC.Api.Grpc.ReglasGrpcException;

namespace Siha.Integracion.Tests;

/// <summary>
/// S2-S03 (US-08) con INFRAESTRUCTURA REAL: el cliente gRPC del Microservicio C contra el servidor
/// gRPC del Microservicio A real (SQL Server 2022 con migraciones y semilla). Comprueba el contrato
/// Protobuf compartido, que un cambio de umbral en A se ve en la siguiente consulta de C (CA2) y que
/// una caída de A produce un error controlado en vez de valores inventados (CA3).
/// </summary>
[Collection(ColeccionInfraestructura.Nombre)]
[Trait("Categoria", "InfraestructuraReal")]
public class ReglasGrpcMsCRealTests
{
    private readonly InfraestructuraReal _infra;
    private readonly ITestOutputHelper _salida;

    public ReglasGrpcMsCRealTests(InfraestructuraReal infra, ITestOutputHelper salida)
    {
        _infra = infra;
        _salida = salida;
    }

    private static ClienteReglasMsC ClienteMsC(Grpc.Net.Client.GrpcChannel canal, int maxRetries = 1) =>
        new(new ClienteGeneradoMsC(canal),
            new Opciones(new OpcionesReglasMsC { TimeoutSegundos = 10, MaxRetries = maxRetries, RetryBaseDelaySegundos = 0 }),
            NullLogger<ClienteReglasMsC>.Instance);

    [Fact]
    public async Task El_cliente_de_C_lee_los_umbrales_vigentes_de_A_y_ve_el_cambio_de_version()
    {
        await using var msa = new MsAReal(_infra);
        using var canal = msa.CanalGrpc();
        var cliente = ClienteMsC(canal);

        var antes = await cliente.ObtenerConfiguracionAsync(RutasNormativas.CambioCarreraOu, TiposTramite.Interno);
        antes!.ParametrosConfigurados.Should().BeTrue();
        antes.Reglas.Should().NotBeEmpty().And.OnlyContain(r => r.TipoTramite == TiposTramite.Interno || r.TipoTramite == "");
        _salida.WriteLine($"[gRPC A→C] antes: θ={antes.Parametros.Theta} δ={antes.Parametros.Delta} τ={antes.Parametros.Tau} v{antes.Parametros.Version}");

        var admin = msa.ClienteCon(RolesSistema.Administrador);
        var cambio = await admin.PutAsJsonAsync(
            $"/api/reglas/parametros/{RutasNormativas.CambioCarreraOu}", new { theta = 82.0, delta = 10.0, tau = 70.0 });
        cambio.StatusCode.Should().Be(HttpStatusCode.OK, await cambio.Content.ReadAsStringAsync());

        var despues = await cliente.ObtenerConfiguracionAsync(RutasNormativas.CambioCarreraOu);
        _salida.WriteLine($"[gRPC A→C] después: θ={despues!.Parametros.Theta} δ={despues.Parametros.Delta} τ={despues.Parametros.Tau} v{despues.Parametros.Version}");

        despues.Parametros.Theta.Should().Be(82.0);
        despues.Parametros.Delta.Should().Be(10.0);
        despues.Parametros.Tau.Should().Be(70.0);
        despues.Parametros.Version.Should().BeGreaterThan(antes.Parametros.Version, "C identifica la versión aplicada (CA2)");
    }

    [Fact]
    public async Task Ruta_inexistente_se_rechaza_con_error_controlado_sin_inventar_parametros()
    {
        await using var msa = new MsAReal(_infra);
        using var canal = msa.CanalGrpc();

        var accion = () => ClienteMsC(canal).ObtenerConfiguracionAsync("CAU-P-999");

        // A valida la ruta y responde InvalidArgument; C no reintenta ni sustituye valores (CA3).
        var ex = (await accion.Should().ThrowAsync<ReglasGrpcExceptionMsC>()).Which;
        _salida.WriteLine($"[gRPC ruta inválida] {ex.StatusCode}: {ex.Message}");
        ex.StatusCode.Should().Be(StatusCode.InvalidArgument);
    }

    [Fact]
    public async Task Con_A_caido_el_cliente_de_C_lanza_error_controlado_tras_reintentar()
    {
        // Puerto sin servidor: equivale a que el contenedor ms-a esté detenido.
        using var canal = Grpc.Net.Client.GrpcChannel.ForAddress("http://127.0.0.1:1");
        var cliente = ClienteMsC(canal, maxRetries: 1);
        var reloj = System.Diagnostics.Stopwatch.StartNew();

        var accion = () => cliente.ObtenerConfiguracionAsync(RutasNormativas.CambioCarreraOu);

        var ex = (await accion.Should().ThrowAsync<ReglasGrpcExceptionMsC>()).Which;
        reloj.Stop();
        _salida.WriteLine($"[gRPC A caído] {ex.StatusCode}: {ex.Message} ({reloj.ElapsedMilliseconds} ms)");
        ex.StatusCode.Should().Be(StatusCode.Unavailable);
        reloj.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30), "no debe quedarse colgado");
    }

    [Fact]
    public async Task Latencia_de_la_consulta_gRPC_real_es_aceptable()
    {
        await using var msa = new MsAReal(_infra);
        using var canal = msa.CanalGrpc();
        var cliente = ClienteMsC(canal);
        await cliente.ObtenerConfiguracionAsync(RutasNormativas.Movilidad); // calentamiento

        var tiempos = new List<double>();
        for (var i = 0; i < 20; i++)
        {
            var reloj = System.Diagnostics.Stopwatch.StartNew();
            await cliente.ObtenerConfiguracionAsync(RutasNormativas.Movilidad);
            tiempos.Add(reloj.Elapsed.TotalMilliseconds);
        }

        tiempos.Sort();
        var p95 = tiempos[(int)Math.Ceiling(tiempos.Count * 0.95) - 1];
        _salida.WriteLine($"[gRPC latencia] mediana={tiempos[tiempos.Count / 2]:F1} ms p95={p95:F1} ms (20 llamadas, SQL Server real)");
        p95.Should().BeLessThan(1000);
    }

    private sealed class Opciones : IOptionsMonitor<OpcionesReglasMsC>
    {
        public Opciones(OpcionesReglasMsC valor) => CurrentValue = valor;
        public OpcionesReglasMsC CurrentValue { get; }
        public OpcionesReglasMsC Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<OpcionesReglasMsC, string?> listener) => null;
    }
}
