using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Grpc.Core;
using Siha.Contracts;
using Siha.Integracion.Tests.Soporte;
using Siha.MsA.Api.Grpc;
using Xunit.Abstractions;

namespace Siha.Integracion.Tests;

/// <summary>
/// Servidor gRPC de reglas (S1-S04) probado con INFRAESTRUCTURA REAL (S1-S09, "pruebas integradas de
/// reglas"): Microservicio A completo con migraciones y semilla en SQL Server 2022, cliente gRPC
/// generado desde el mismo <c>reglas.proto</c> y comparación contra la API REST de reglas, que es la
/// otra cara de la misma fuente de verdad (<c>IReglasService</c>).
/// </summary>
[Collection(ColeccionInfraestructura.Nombre)]
[Trait("Categoria", "InfraestructuraReal")]
public class ReglasGrpcRealTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly InfraestructuraReal _infra;
    private readonly ITestOutputHelper _salida;

    public ReglasGrpcRealTests(InfraestructuraReal infra, ITestOutputHelper salida)
    {
        _infra = infra;
        _salida = salida;
    }

    [Theory]
    [InlineData(RutasNormativas.CambioCarreraOu)]
    [InlineData(RutasNormativas.Movilidad)]
    public async Task Grpc_devuelve_los_umbrales_y_reglas_sembrados_en_SQL_Server_igual_que_REST(string ruta)
    {
        await using var ms = new MsAReal(_infra);
        using var canal = ms.CanalGrpc();
        var cliente = new ReglasGrpc.ReglasGrpcClient(canal);

        var respuesta = await cliente.ObtenerConfiguracionRutaAsync(new SolicitudConfiguracionRuta { RutaNormativa = ruta });
        _salida.WriteLine($"[gRPC real] {ruta}: θ={respuesta.Parametros?.Theta} δ={respuesta.Parametros?.Delta} τ={respuesta.Parametros?.Tau} v{respuesta.Parametros?.Version} reglas={respuesta.Reglas.Count}");

        respuesta.RutaNormativa.Should().Be(ruta);
        respuesta.ParametrosConfigurados.Should().BeTrue("la semilla de DB A registra θ, δ, τ para las dos rutas");
        respuesta.Parametros.Should().NotBeNull();
        respuesta.Parametros!.Delta.Should().BeLessThanOrEqualTo(respuesta.Parametros.Theta, "invariante δ ≤ θ");
        new[] { respuesta.Parametros.Theta, respuesta.Parametros.Delta, respuesta.Parametros.Tau }
            .Should().OnlyContain(v => v >= 0 && v <= 100);
        respuesta.Reglas.Should().NotBeEmpty().And.OnlyContain(r => r.RutaNormativa == ruta);

        // Misma fuente de verdad: la API REST devuelve exactamente lo mismo.
        var rest = ms.ClienteCon(RolesSistema.Operador);
        var parametrosRest = await rest.GetFromJsonAsync<JsonElement>($"/api/reglas/parametros/{ruta}", Json);
        respuesta.Parametros.Theta.Should().Be(parametrosRest.GetProperty("theta").GetDouble());
        respuesta.Parametros.Delta.Should().Be(parametrosRest.GetProperty("delta").GetDouble());
        respuesta.Parametros.Tau.Should().Be(parametrosRest.GetProperty("tau").GetDouble());
        respuesta.Parametros.Version.Should().Be(parametrosRest.GetProperty("version").GetInt32());

        var reglasRest = await rest.GetFromJsonAsync<JsonElement[]>($"/api/reglas?rutaNormativa={ruta}", Json);
        respuesta.Reglas.Select(r => r.Codigo).Should().BeEquivalentTo(reglasRest!.Select(r => r.GetProperty("codigo").GetString()));
    }

    [Fact]
    public async Task Grpc_filtra_por_tipo_de_tramite()
    {
        await using var ms = new MsAReal(_infra);
        using var canal = ms.CanalGrpc();
        var cliente = new ReglasGrpc.ReglasGrpcClient(canal);

        var externas = await cliente.ObtenerConfiguracionRutaAsync(new SolicitudConfiguracionRuta
        {
            RutaNormativa = RutasNormativas.Movilidad,
            TipoTramite = TiposTramite.Externo
        });

        externas.Reglas.Should().NotBeEmpty().And.OnlyContain(r => r.TipoTramite == TiposTramite.Externo);

        var rest = ms.ClienteCon(RolesSistema.Operador);
        var reglasRest = await rest.GetFromJsonAsync<JsonElement[]>(
            $"/api/reglas?rutaNormativa={RutasNormativas.Movilidad}&tipoTramite={TiposTramite.Externo}", Json);
        externas.Reglas.Select(r => r.Codigo).Should().BeEquivalentTo(reglasRest!.Select(r => r.GetProperty("codigo").GetString()));
    }

    [Theory]
    [InlineData("CAU-P-999", "")]
    [InlineData("", "")]
    [InlineData(RutasNormativas.CambioCarreraOu, "EXTRANJERO")]
    public async Task Grpc_rechaza_ruta_o_tipo_invalidos_con_InvalidArgument(string ruta, string tipo)
    {
        await using var ms = new MsAReal(_infra);
        using var canal = ms.CanalGrpc();
        var cliente = new ReglasGrpc.ReglasGrpcClient(canal);

        var llamada = async () => await cliente.ObtenerConfiguracionRutaAsync(
            new SolicitudConfiguracionRuta { RutaNormativa = ruta, TipoTramite = tipo });

        var error = await llamada.Should().ThrowAsync<RpcException>();
        error.Which.StatusCode.Should().Be(StatusCode.InvalidArgument);
        _salida.WriteLine($"[gRPC real] ruta='{ruta}' tipo='{tipo}' -> {error.Which.StatusCode}: {error.Which.Status.Detail}");
    }

    [Fact]
    public async Task Un_cambio_de_umbrales_por_REST_se_refleja_en_gRPC_y_persiste_en_SQL_Server()
    {
        await using var ms = new MsAReal(_infra);
        using var canal = ms.CanalGrpc();
        var cliente = new ReglasGrpc.ReglasGrpcClient(canal);
        var ruta = RutasNormativas.CambioCarreraOu;

        var antes = await cliente.ObtenerConfiguracionRutaAsync(new SolicitudConfiguracionRuta { RutaNormativa = ruta });

        var admin = ms.ClienteCon(RolesSistema.Administrador);
        var cambio = await admin.PutAsJsonAsync($"/api/reglas/parametros/{ruta}", new { theta = 77.5, delta = 7.5, tau = 66.0 });
        cambio.StatusCode.Should().Be(HttpStatusCode.OK, await cambio.Content.ReadAsStringAsync());

        // Un cambio inválido (δ > θ) se rechaza y no altera lo que ve gRPC.
        var invalido = await admin.PutAsJsonAsync($"/api/reglas/parametros/{ruta}", new { theta = 50.0, delta = 60.0, tau = 66.0 });
        invalido.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var despues = await cliente.ObtenerConfiguracionRutaAsync(new SolicitudConfiguracionRuta { RutaNormativa = ruta });
        _salida.WriteLine($"[gRPC real] antes θ={antes.Parametros.Theta} v{antes.Parametros.Version} -> después θ={despues.Parametros.Theta} δ={despues.Parametros.Delta} τ={despues.Parametros.Tau} v{despues.Parametros.Version} por {despues.Parametros.ActualizadoPor}");

        despues.Parametros.Theta.Should().Be(77.5);
        despues.Parametros.Delta.Should().Be(7.5);
        despues.Parametros.Tau.Should().Be(66.0);
        despues.Parametros.Version.Should().BeGreaterThan(antes.Parametros.Version);

        // Persistencia real: un MsA nuevo sobre la misma base devuelve los mismos valores.
        await using var otraInstancia = new MsAReal(_infra, ms.BaseDatos);
        using var canal2 = otraInstancia.CanalGrpc();
        var releida = await new ReglasGrpc.ReglasGrpcClient(canal2)
            .ObtenerConfiguracionRutaAsync(new SolicitudConfiguracionRuta { RutaNormativa = ruta });
        releida.Parametros.Theta.Should().Be(77.5);
        releida.Parametros.Version.Should().Be(despues.Parametros.Version);
    }
}
