using FluentAssertions;
using Newtonsoft.Json;
using Ocelot.Configuration.File;
using Siha.MsB.Tests.Soporte;

namespace Siha.MsB.Tests;

/// <summary>
/// Pruebas de integración del API Gateway (US-04, tareas S1-S07 / S1-S09).
///
/// Se valida la configuración real de Ocelot (§Routes de ocelot.json y ocelot.Development.json)
/// deserializándola con los mismos tipos que usa el Gateway, y se comprueba que los hosts de
/// producción coinciden con la red de docker-compose. No se levanta el proceso del Gateway porque
/// su <c>Program</c> no es público y está fuera del alcance autorizado, pero la configuración es
/// la que determina el enrutamiento y el control de acceso en el borde.
/// </summary>
public class GatewayOcelotConfiguracionTests
{
    private const string RutaProduccion = "ocelot.json";
    private const string RutaDesarrollo = "ocelot.Development.json";

    private static string RaizRepositorio => Pruebas.RaizRepositorio();

    private static FileConfiguration Cargar(string archivo)
    {
        var ruta = Path.Combine(RaizRepositorio, "src", "Services", "Siha.Gateway", archivo);
        File.Exists(ruta).Should().BeTrue($"el Gateway debe exponer su configuración {archivo}");

        var configuracion = JsonConvert.DeserializeObject<FileConfiguration>(File.ReadAllText(ruta));
        configuracion.Should().NotBeNull($"'{archivo}' debe ser un JSON válido para Ocelot");
        return configuracion!;
    }

    /// <summary>
    /// Devuelve la clave del proveedor de autenticación declarada en el JSON. La propiedad singular
    /// está obsoleta en Ocelot 23 (a favor de <c>AuthenticationProviderKeys</c>), pero es la que
    /// declaran explícitamente ocelot.json y ocelot.Development.json del proyecto.
    /// </summary>
    private static string? ClaveAutenticacion(FileRoute ruta)
    {
#pragma warning disable CS0618
        return ruta.AuthenticationOptions.AuthenticationProviderKey
            ?? ruta.AuthenticationOptions.AuthenticationProviderKeys?.FirstOrDefault();
#pragma warning restore CS0618
    }

    [Theory]
    [InlineData(RutaProduccion)]
    [InlineData(RutaDesarrollo)]
    public void La_configuracion_de_ocelot_es_estructuralmente_valida(string archivo)
    {
        var configuracion = Cargar(archivo);

        configuracion.GlobalConfiguration.BaseUrl.Should().Be("http://localhost:5000");
        configuracion.Routes.Should().NotBeNullOrEmpty();

        configuracion.Routes.Should().OnlyContain(r => !string.IsNullOrWhiteSpace(r.UpstreamPathTemplate));
        configuracion.Routes.Should().OnlyContain(r => !string.IsNullOrWhiteSpace(r.DownstreamPathTemplate));
        configuracion.Routes.Should().OnlyContain(r => r.UpstreamHttpMethod.Count > 0);
        configuracion.Routes.Should().OnlyContain(r => r.DownstreamHostAndPorts.Count > 0);
        configuracion.Routes.Should().OnlyContain(r => r.DownstreamHostAndPorts.All(h => h.Port > 0));
    }

    [Fact]
    public void Produccion_enruta_los_microservicios_por_su_nombre_en_docker()
    {
        var configuracion = Cargar(RutaProduccion);

        var expedientes = configuracion.Routes
            .Where(r => r.UpstreamPathTemplate.StartsWith("/api/expedientes", StringComparison.Ordinal))
            .ToList();

        expedientes.Should().HaveCount(2);
        expedientes.Should().OnlyContain(r => r.DownstreamHostAndPorts.First().Host == "ms-b");
        expedientes.Should().OnlyContain(r => r.DownstreamHostAndPorts.First().Port == 8080);

        // El POST de documentos (multipart) también debe poder cruzar el Gateway.
        expedientes.Should().OnlyContain(r => r.UpstreamHttpMethod.Contains("POST"));

        var reglas = configuracion.Routes.First(r =>
            r.UpstreamPathTemplate == "/api/reglas" && r.UpstreamHttpMethod.Contains("GET"));

        reglas.DownstreamHostAndPorts.First().Host.Should().Be("ms-a");
        reglas.DownstreamHostAndPorts.First().Port.Should().Be(8080);
    }

    [Fact]
    public void Desarrollo_enruta_alos_puertos_locales_de_cada_microservicio()
    {
        var configuracion = Cargar(RutaDesarrollo);

        var servicios = configuracion.Routes
            .SelectMany(r => r.DownstreamHostAndPorts)
            .Select(h => $"{h.Host}:{h.Port}")
            .Distinct()
            .ToList();

        servicios.Should().OnlyContain(s => s.StartsWith("localhost:"));
        servicios.Should().Contain("localhost:5081", "el Microservicio A escucha en 5081 en local");
        servicios.Should().Contain("localhost:5082", "el Microservicio B escucha en 5082 en local");
    }

    [Fact]
    public void Las_rutas_protegidas_exigen_el_esquema_bearer_en_el_borde()
    {
        var configuracion = Cargar(RutaProduccion);

        // El login es la única ruta pública.
        configuracion.Routes
            .Where(r => r.UpstreamPathTemplate == "/api/auth/login")
            .Should().OnlyContain(r => ClaveAutenticacion(r) == null);

        var protegidas = configuracion.Routes
            .Where(r => r.UpstreamPathTemplate != "/api/auth/login")
            .ToList();

        protegidas.Should().NotBeEmpty();
        protegidas.Should().OnlyContain(r => ClaveAutenticacion(r) == "Bearer");
    }

    [Fact]
    public void Las_operaciones_administrativas_exigen_el_rol_administrador()
    {
        var configuracion = Cargar(RutaProduccion);

        string[] recursosDeGobierno =
        {
            "/api/usuarios", "/api/roles", "/api/reglas", "/api/carreras", "/api/mallas", "/api/asignaturas"
        };

        var administrativas = configuracion.Routes
            .Where(r => recursosDeGobierno.Any(recurso =>
                r.UpstreamPathTemplate.StartsWith(recurso, StringComparison.Ordinal)))
            .Where(r => r.UpstreamHttpMethod.Any(m => m is "POST" or "PUT" or "PATCH" or "DELETE"))
            .ToList();

        administrativas.Should().NotBeEmpty();
        administrativas.Should().OnlyContain(r =>
            r.RouteClaimsRequirement != null &&
            r.RouteClaimsRequirement.Values.Contains("ADMINISTRADOR"));

        // El registro de expedientes lo realiza el OPERADOR: no puede exigir ADMINISTRADOR.
        var expedientes = configuracion.Routes
            .Where(r => r.UpstreamPathTemplate.StartsWith("/api/expedientes", StringComparison.Ordinal))
            .ToList();

        expedientes.Should().NotBeEmpty();
        expedientes.Should().OnlyContain(r => r.RouteClaimsRequirement == null ||
            !r.RouteClaimsRequirement.Values.Contains("ADMINISTRADOR"));
    }

    [Fact]
    public void Los_hosts_de_produccion_existen_en_la_red_de_docker_compose()
    {
        var compose = File.ReadAllText(Path.Combine(RaizRepositorio, "docker-compose.yml"));
        var configuracion = Cargar(RutaProduccion);

        var hosts = configuracion.Routes
            .SelectMany(r => r.DownstreamHostAndPorts)
            .Select(h => h.Host)
            .Distinct()
            .ToList();

        // Los dos únicos hosts de producción son los servicios definidos en docker-compose.
        hosts.Should().BeEquivalentTo(new[] { "ms-a", "ms-b" });

        var dockerfiles = new Dictionary<string, string>
        {
            ["ms-a"] = Path.Combine("src", "Services", "Siha.MsA.Api", "Dockerfile"),
            ["ms-b"] = Path.Combine("src", "Services", "Siha.MsB.Api", "Dockerfile")
        };

        foreach (var host in hosts)
        {
            compose.Should().MatchRegex(
                $@"(?m)^\s{{2}}{System.Text.RegularExpressions.Regex.Escape(host)}:\s*$",
                $"el servicio '{host}' debe existir en docker-compose.yml");

            // ...y cada microservicio escucha en el puerto 8080 dentro de la red.
            var dockerfile = File.ReadAllText(Path.Combine(RaizRepositorio, dockerfiles[host]));
            dockerfile.Should().Contain("ASPNETCORE_URLS=http://+:8080");
            dockerfile.Should().Contain("EXPOSE 8080");
        }

        // El Gateway publica las rutas en el puerto 5000 y depende de ambos microservicios.
        compose.Should().Contain("- \"5000:8080\"");
    }

    [Fact]
    public void La_configuracion_de_expedientes_del_gateway_cubre_la_carga_de_documentos()
    {
        var configuracion = Cargar(RutaDesarrollo);

        var documentos = configuracion.Routes.FirstOrDefault(r =>
            r.UpstreamPathTemplate == "/api/expedientes/{everything}");

        documentos.Should().NotBeNull();
        documentos!.UpstreamHttpMethod.Should().Contain("POST");
        documentos.DownstreamPathTemplate.Should().Be("/api/expedientes/{everything}");
    }
}
