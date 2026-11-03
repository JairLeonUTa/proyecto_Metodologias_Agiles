using FluentAssertions;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Siha.MsA.Api.Grpc;
using Siha.MsC.Api.Grpc;
using Siha.MsC.Api.Options;

namespace Siha.MsC.Tests;

/// <summary>
/// S2-S03 (US-08): contrato del cliente gRPC del Microservicio C hacia ReglasGrpc (Microservicio A).
/// Usa un <see cref="CallInvoker"/> programable: no abre red, pero ejercita el cliente generado desde
/// reglas.proto, el deadline, los reintentos y el mapeo de errores a <see cref="ReglasGrpcException"/>.
/// La prueba contra el Microservicio A real está en Siha.Integracion.Tests (ReglasGrpcMsCRealTests).
/// </summary>
public class ReglasGrpcClientTests
{
    private static ConfiguracionRuta Configuracion(string ruta = "CAU-P-011") => new()
    {
        RutaNormativa = ruta,
        ParametrosConfigurados = true,
        Parametros = new ParametrosEvaluacionMensaje { RutaNormativa = ruta, Theta = 80, Delta = 15, Tau = 75, Version = 3 },
        Reglas = { new ReglaMensaje { Codigo = "RN-01", RutaNormativa = ruta, Activa = true } }
    };

    private static (Siha.MsC.Api.Grpc.ReglasGrpcClient cliente, InvocadorProgramable invocador) Crear(
        int maxRetries = 2, int timeoutSegundos = 10, params Func<SolicitudConfiguracionRuta, Task<ConfiguracionRuta>>[] respuestas)
    {
        var invocador = new InvocadorProgramable(respuestas);
        var opciones = new OpcionesFijas(new ReglasGrpcOptions
        {
            MaxRetries = maxRetries,
            TimeoutSegundos = timeoutSegundos,
            RetryBaseDelaySegundos = 0
        });
        var cliente = new Siha.MsC.Api.Grpc.ReglasGrpcClient(
            new ReglasGrpc.ReglasGrpcClient(invocador), opciones, NullLogger<Siha.MsC.Api.Grpc.ReglasGrpcClient>.Instance);
        return (cliente, invocador);
    }

    private static Func<SolicitudConfiguracionRuta, Task<ConfiguracionRuta>> Ok(ConfiguracionRuta r) => _ => Task.FromResult(r);

    private static Func<SolicitudConfiguracionRuta, Task<ConfiguracionRuta>> Falla(StatusCode codigo) =>
        _ => Task.FromException<ConfiguracionRuta>(new RpcException(new Status(codigo, $"simulado {codigo}")));

    [Fact]
    public async Task Caso1_exito_devuelve_umbrales_y_envia_ruta_y_tipo()
    {
        var (cliente, invocador) = Crear(respuestas: Ok(Configuracion()));

        var r = await cliente.ObtenerConfiguracionAsync("CAU-P-011", "INTERNO");

        r.Should().NotBeNull();
        r!.Parametros.Theta.Should().Be(80);
        r.Parametros.Delta.Should().Be(15);
        r.Parametros.Tau.Should().Be(75);
        r.Parametros.Version.Should().Be(3, "la versión de la regla aplicada viaja en la respuesta (CA2)");
        invocador.Llamadas.Should().Be(1);
        invocador.UltimaSolicitud!.RutaNormativa.Should().Be("CAU-P-011");
        invocador.UltimaSolicitud.TipoTramite.Should().Be("INTERNO");
    }

    [Fact]
    public async Task Caso2_servidor_caido_una_vez_reintenta_y_recupera()
    {
        var (cliente, invocador) = Crear(respuestas: new[] { Falla(StatusCode.Unavailable), Ok(Configuracion()) });

        var r = await cliente.ObtenerConfiguracionAsync("CAU-P-011");

        r!.ParametrosConfigurados.Should().BeTrue();
        invocador.Llamadas.Should().Be(2);
    }

    [Fact]
    public async Task Caso3_timeout_persistente_agota_reintentos_y_lanza_excepcion_controlada()
    {
        var (cliente, invocador) = Crear(maxRetries: 2, respuestas: Falla(StatusCode.DeadlineExceeded));

        var accion = () => cliente.ObtenerConfiguracionAsync("CAU-P-011");

        var ex = (await accion.Should().ThrowAsync<ReglasGrpcException>()).Which;
        ex.StatusCode.Should().Be(StatusCode.DeadlineExceeded);
        invocador.Llamadas.Should().Be(3, "1 intento + 2 reintentos; nunca inventa valores");
    }

    [Fact]
    public async Task Caso4_payload_invalido_no_reintenta()
    {
        var (cliente, invocador) = Crear(respuestas: Falla(StatusCode.InvalidArgument));

        var accion = () => cliente.ObtenerConfiguracionAsync("");

        (await accion.Should().ThrowAsync<ReglasGrpcException>()).Which.StatusCode.Should().Be(StatusCode.InvalidArgument);
        invocador.Llamadas.Should().Be(1);
    }

    [Fact]
    public async Task Caso5_version_de_contrato_incompatible_no_reintenta()
    {
        // Un servidor con otro contrato responde Unimplemented para el método.
        var (cliente, invocador) = Crear(respuestas: Falla(StatusCode.Unimplemented));

        var accion = () => cliente.ObtenerConfiguracionAsync("CAU-P-011");

        (await accion.Should().ThrowAsync<ReglasGrpcException>()).Which.StatusCode.Should().Be(StatusCode.Unimplemented);
        invocador.Llamadas.Should().Be(1);
    }

    [Fact]
    public async Task Caso6_ruta_sin_parametros_se_devuelve_tal_cual_sin_rellenar_valores()
    {
        var vacia = new ConfiguracionRuta { RutaNormativa = "CAU-P-999", ParametrosConfigurados = false };
        var (cliente, _) = Crear(respuestas: Ok(vacia));

        var r = await cliente.ObtenerConfiguracionAsync("CAU-P-999");

        r!.ParametrosConfigurados.Should().BeFalse();
        r.Parametros.Should().BeNull("el cliente no sustituye silenciosamente θ, δ, τ (CA3)");
        r.Reglas.Should().BeEmpty();
    }

    [Fact]
    public async Task Caso7_cancelacion_se_propaga_sin_reintentar()
    {
        var (cliente, invocador) = Crear(respuestas: Ok(Configuracion()));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var accion = () => cliente.ObtenerConfiguracionAsync("CAU-P-011", cancellationToken: cts.Token);

        await accion.Should().ThrowAsync<OperationCanceledException>();
        invocador.Llamadas.Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task Cada_llamada_lleva_un_deadline_segun_TimeoutSegundos()
    {
        var (cliente, invocador) = Crear(timeoutSegundos: 7, respuestas: Ok(Configuracion()));
        var antes = DateTime.UtcNow;

        await cliente.ObtenerConfiguracionAsync("CAU-P-011");

        invocador.UltimoDeadline.Should().NotBeNull();
        invocador.UltimoDeadline!.Value.Should().BeCloseTo(antes.AddSeconds(7), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Error_inesperado_del_transporte_se_envuelve_en_ReglasGrpcException()
    {
        var (cliente, _) = Crear(respuestas: _ => Task.FromException<ConfiguracionRuta>(new HttpRequestException("socket cerrado")));

        var accion = () => cliente.ObtenerConfiguracionAsync("CAU-P-011");

        (await accion.Should().ThrowAsync<ReglasGrpcException>()).Which.InnerException.Should().BeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task Latencia_del_cliente_sin_reintentos_es_baja()
    {
        var (cliente, _) = Crear(respuestas: Ok(Configuracion()));
        var reloj = System.Diagnostics.Stopwatch.StartNew();

        for (var i = 0; i < 50; i++)
        {
            await cliente.ObtenerConfiguracionAsync("CAU-P-011");
        }

        reloj.Stop();
        (reloj.Elapsed / 50).Should().BeLessThan(TimeSpan.FromMilliseconds(50), "el cliente no añade esperas fuera de los reintentos");
    }

    /// <summary>CallInvoker que devuelve respuestas programadas en orden (la última se repite).</summary>
    private sealed class InvocadorProgramable : CallInvoker
    {
        private readonly Func<SolicitudConfiguracionRuta, Task<ConfiguracionRuta>>[] _respuestas;

        public InvocadorProgramable(Func<SolicitudConfiguracionRuta, Task<ConfiguracionRuta>>[] respuestas) => _respuestas = respuestas;

        public int Llamadas { get; private set; }
        public SolicitudConfiguracionRuta? UltimaSolicitud { get; private set; }
        public DateTime? UltimoDeadline { get; private set; }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            var solicitud = (SolicitudConfiguracionRuta)(object)request;
            UltimaSolicitud = solicitud;
            UltimoDeadline = options.Deadline;
            var respuesta = _respuestas[Math.Min(Llamadas, _respuestas.Length - 1)](solicitud);
            Llamadas++;

            return new AsyncUnaryCall<TResponse>(
                respuesta.ContinueWith(t => (TResponse)(object)t.GetAwaiter().GetResult(), TaskScheduler.Default),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
        }

        public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
            => throw new NotSupportedException();

        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
            => throw new NotSupportedException();

        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options)
            => throw new NotSupportedException();

        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options)
            => throw new NotSupportedException();
    }

    private sealed class OpcionesFijas : IOptionsMonitor<ReglasGrpcOptions>
    {
        public OpcionesFijas(ReglasGrpcOptions valor) => CurrentValue = valor;
        public ReglasGrpcOptions CurrentValue { get; }
        public ReglasGrpcOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<ReglasGrpcOptions, string?> listener) => null;
    }
}
