using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Siha.Contracts;
using Siha.MsA.Api.Contracts.Dtos;
using Siha.MsA.Api.Controllers;
using Siha.MsA.Api.Services;

namespace Siha.MsA.Tests;

/// <summary>
/// Pruebas de control de acceso basado en roles (RBAC) y de la validación de umbrales
/// (Sprint 1 · tareas S1-P02, S1-P03, S1-P07 y S1-P08 del PO Proxy / QA).
///
/// Verifican por reflexión sobre los atributos <c>[Authorize]</c> que el Microservicio A
/// exige el rol ADMINISTRADOR en la gestión de cuentas, roles, reglas y catálogo, que las
/// consultas exigen autenticación sin rol específico, y que el login es anónimo.
/// Complementan —sin modificar— las pruebas existentes de <see cref="SeguridadTests"/>,
/// <see cref="ParametrosValidatorTests"/> y <see cref="CatalogoServiceTests"/>.
/// </summary>
public class RbacTests
{
    private static readonly Assembly EnsambladoApi =
        typeof(AuthController).Assembly;

    private static readonly string[] VerbosDeEscritura =
    {
        "HttpPostAttribute", "HttpPutAttribute", "HttpPatchAttribute", "HttpDeleteAttribute"
    };

    // ------------------------------------------------------------------------
    //  Catálogo de controladores del Microservicio A
    // ------------------------------------------------------------------------

    private static IEnumerable<Type> Controladores() =>
        EnsambladoApi.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract && t.IsPublic);

    private static IEnumerable<MethodInfo> Acciones(Type controlador) =>
        controlador
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName);

    private static IEnumerable<string> RolesDe(MemberInfo miembro) =>
        miembro.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .SelectMany(a => (a.Roles ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>Roles que el endpoint admite efectivamente (atributo del controlador + de la acción).</summary>
    private static IEnumerable<string> RolesEfectivos(Type controlador, MethodInfo accion) =>
        RolesDe(controlador).Concat(RolesDe(accion)).Distinct(StringComparer.OrdinalIgnoreCase);

    private static bool ExigeAutenticacion(params MemberInfo[] miembros) =>
        miembros.Any(m => m.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any());

    private static bool EsAnonimo(MethodInfo accion) =>
        accion.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any();

    // ------------------------------------------------------------------------
    //  RBAC: quién puede hacer qué (US-01, US-02, US-03 y borde US-04)
    // ------------------------------------------------------------------------

    [Fact]
    public void El_microservicio_A_expone_controladores_con_API_Controller_y_ruta()
    {
        var controladores = Controladores().ToList();

        controladores.Should().NotBeEmpty();
        controladores.Select(c => c.Name).Should().BeEquivalentTo(new[]
        {
            "AsignaturasController", "AuthController", "CarrerasController",
            "MallasController", "ReglasController", "RolesController", "UsuariosController"
        });

        foreach (var controlador in controladores)
        {
            controlador.GetCustomAttributes<ApiControllerAttribute>(inherit: true)
                .Should().NotBeEmpty($"{controlador.Name} debe estar marcado con [ApiController]");

            controlador.GetCustomAttributes<RouteAttribute>(inherit: true)
                .Should().NotBeEmpty($"{controlador.Name} debe declarar [Route]");
        }
    }

    [Theory]
    [InlineData(typeof(UsuariosController))]
    [InlineData(typeof(RolesController))]
    public void La_gestion_de_cuentas_y_roles_exige_el_rol_administrador(Type controlador)
    {
        ExigeAutenticacion(controlador).Should().BeTrue(
            $"{controlador.Name} no puede quedar accesible sin autenticación");

        RolesDe(controlador).Should().Contain(RolesSistema.Administrador,
            $"{controlador.Name} solo debe ser accesible por ADMINISTRADOR (US-01, CA3)");
    }

    [Theory]
    [InlineData(typeof(CarrerasController), nameof(CarrerasController.Crear))]
    [InlineData(typeof(CarrerasController), nameof(CarrerasController.Actualizar))]
    [InlineData(typeof(MallasController), nameof(MallasController.Crear))]
    [InlineData(typeof(AsignaturasController), nameof(AsignaturasController.Crear))]
    [InlineData(typeof(AsignaturasController), nameof(AsignaturasController.Actualizar))]
    [InlineData(typeof(AsignaturasController), nameof(AsignaturasController.Importar))]
    [InlineData(typeof(ReglasController), nameof(ReglasController.Crear))]
    [InlineData(typeof(ReglasController), nameof(ReglasController.ActualizarParametros))]
    public void La_escritura_del_catalogo_y_de_las_reglas_exige_el_rol_administrador(Type controlador, string accion)
    {
        var metodo = controlador.GetMethod(accion);

        metodo.Should().NotBeNull($"{controlador.Name}.{accion} debe existir");

        RolesEfectivos(controlador, metodo!).Should().Contain(RolesSistema.Administrador,
            $"{controlador.Name}.{accion} solo debe ser accesible por ADMINISTRADOR (US-02/CA3 y US-03)");
    }

    [Theory]
    [InlineData(typeof(ReglasController))]
    [InlineData(typeof(CarrerasController))]
    [InlineData(typeof(MallasController))]
    [InlineData(typeof(AsignaturasController))]
    public void Las_consultas_del_catalogo_exigen_autenticacion_sin_rol_especifico(Type controlador)
    {
        ExigeAutenticacion(controlador).Should().BeTrue(
            $"{controlador.Name} debe exigir un token válido en el borde (US-04, CA1)");

        RolesDe(controlador).Should().BeEmpty(
            $"la consulta de {controlador.Name} está disponible para cualquier rol autenticado (ADMINISTRADOR, DOCENTE u OPERADOR)");
    }

    [Fact]
    public void El_login_es_anonimo_y_la_consulta_de_identidad_exige_token()
    {
        var login = typeof(AuthController).GetMethod(nameof(AuthController.Login));
        var identidad = typeof(AuthController).GetMethod(nameof(AuthController.Yo));

        login.Should().NotBeNull();
        identidad.Should().NotBeNull();

        EsAnonimo(login!).Should().BeTrue("el login debe permitir el acceso sin token (US-01, CA2)");
        EsAnonimo(identidad!).Should().BeFalse("la consulta de identidad exige un token válido");
        RolesEfectivos(typeof(AuthController), identidad!).Should().NotContain(RolesSistema.Administrador);
    }

    [Fact]
    public void Ningun_endpoint_de_escritura_queda_sin_el_rol_administrador()
    {
        var infractores = Controladores()
            .SelectMany(controlador => Acciones(controlador)
                .Where(accion => !EsAnonimo(accion))
                .Where(accion => accion.GetCustomAttributes(inherit: true)
                    .Any(a => VerbosDeEscritura.Contains(a.GetType().Name)))
                .Where(accion => !RolesEfectivos(controlador, accion)
                    .Contains(RolesSistema.Administrador, StringComparer.OrdinalIgnoreCase))
                .Select(accion => $"{controlador.Name}.{accion.Name}"))
            .ToList();

        infractores.Should().BeEmpty(
            "solo el ADMINISTRADOR puede crear o modificar cuentas, roles, reglas y catálogo (US-01/CA3, US-02/CA3, US-03)");
    }

    [Fact]
    public void Los_roles_declarados_en_los_controladores_pertenecen_al_catalogo_de_roles()
    {
        var roles = Controladores()
            .SelectMany(controlador => RolesDe(controlador)
                .Concat(Acciones(controlador).SelectMany(RolesDe)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        roles.Should().Contain(RolesSistema.Administrador);
        roles.Should().OnlyContain(rol => RolesSistema.Todos.Contains(rol),
            "los roles del borde (Ocelot) y de los controladores deben coincidir con RolesSistema");
    }

    [Fact]
    public void El_contrato_de_roles_y_estados_expediente_es_el_esperado()
    {
        RolesSistema.Todos.Should().BeEquivalentTo(
            new[] { "ADMINISTRADOR", "DOCENTE", "OPERADOR" });

        ExpedienteEstados.FalloPublicacion.Should().Be("REGISTRADO_PENDIENTE_PUBLICACION");
        RutasNormativas.Todas.Should().BeEquivalentTo(new[] { "CAU-P-011", "CAU-P-122" });
        TiposTramite.Todos.Should().BeEquivalentTo(new[] { "INTERNO", "EXTERNO" });
    }

    // ------------------------------------------------------------------------
    //  Validación de umbrales θ, δ, τ (US-02, CA2)
    // ------------------------------------------------------------------------

    [Theory]
    [InlineData(80d, 10d, 70d)]
    [InlineData(0d, 0d, 0d)]
    [InlineData(100d, 100d, 100d)]
    [InlineData(75d, 15d, 65d)]
    public void Acepta_parametros_dentro_de_rango_con_banda_menor_o_igual_al_umbral(double theta, double delta, double tau)
    {
        var (valido, error) = ParametrosValidator.Validar(theta, delta, tau);

        valido.Should().BeTrue();
        error.Should().BeNull();
    }

    [Theory]
    [InlineData(50d, 80d, 60d)]   // δ > θ
    [InlineData(60d, 75d, 50d)]   // δ > θ (caso del README de Sprint 1)
    [InlineData(0d, 0.5d, 0d)]    // δ > θ con umbral cero
    public void Rechaza_la_banda_de_tolerancia_mayor_que_el_umbral(double theta, double delta, double tau)
    {
        var (valido, error) = ParametrosValidator.Validar(theta, delta, tau);

        valido.Should().BeFalse();
        error.Should().NotBeNull();
        error.Should().Contain("δ");
    }

    [Theory]
    [InlineData(-1d, 0d, 0d)]      // θ negativo
    [InlineData(0d, -0.5d, 0d)]    // δ negativo
    [InlineData(50d, 10d, -3d)]    // τ negativo
    [InlineData(101d, 0d, 0d)]     // θ > 100 %
    [InlineData(50d, 50.1d, 50d)]  // δ > 100 %
    [InlineData(50d, 10d, 120d)]   // τ > 100 %
    public void Rechaza_valores_negativos_o_mayores_a_cien_por_ciento(double theta, double delta, double tau)
    {
        var (valido, error) = ParametrosValidator.Validar(theta, delta, tau);

        valido.Should().BeFalse();
        error.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(double.NaN, 10d, 50d)]
    [InlineData(50d, double.NaN, 50d)]
    [InlineData(50d, 10d, double.PositiveInfinity)]
    public void Rechaza_valores_no_finitos(double theta, double delta, double tau)
    {
        var (valido, error) = ParametrosValidator.Validar(theta, delta, tau);

        valido.Should().BeFalse();
        error.Should().Contain("finitos");
    }

    [Fact]
    public void Los_limites_del_validador_son_los_del_contrato_de_umbrales()
    {
        ParametrosValidator.Minimo.Should().Be(0d);
        ParametrosValidator.Maximo.Should().Be(100d);
    }

    // ------------------------------------------------------------------------
    //  Comportamiento del controlador de reglas frente a roles/valores inválidos
    // ------------------------------------------------------------------------

    [Fact]
    public async Task El_controlador_de_reglas_traduce_en_400_el_rechazo_de_una_banda_invalida()
    {
        const string mensaje = "La banda de tolerancia δ no puede ser mayor que el umbral θ (δ ≤ θ).";
        var servicio = new Mock<IReglasService>();
        servicio
            .Setup(s => s.ActualizarParametrosAsync(
                RutasNormativas.CambioCarreraOu,
                It.IsAny<ActualizarParametrosRequest>(),
                It.IsAny<string>()))
            .ReturnsAsync((false, mensaje, null));

        var controlador = CrearControladorDeReglas(servicio.Object);

        var resultado = await controlador.ActualizarParametros(
            RutasNormativas.CambioCarreraOu,
            new ActualizarParametrosRequest(50d, 80d, 60d));

        var badRequest = resultado.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().BeEquivalentTo(new { mensaje });
        servicio.Verify(
            s => s.ActualizarParametrosAsync(
                RutasNormativas.CambioCarreraOu,
                It.Is<ActualizarParametrosRequest>(r => r.Theta == 50d && r.Delta == 80d && r.Tau == 60d),
                "admin@uta.edu.ec"),
            Times.Once);
    }

    [Fact]
    public async Task El_controlador_de_reglas_oculta_los_parametros_inexistentes_con_404()
    {
        var servicio = new Mock<IReglasService>();
        servicio
            .Setup(s => s.ObtenerParametrosAsync("CAU-P-999"))
            .ReturnsAsync((ParametroEvaluacionDto?)null);

        var controlador = CrearControladorDeReglas(servicio.Object);

        var resultado = await controlador.ObtenerParametros("CAU-P-999");

        resultado.Result.Should().BeOfType<NotFoundObjectResult>();
        servicio.Verify(s => s.ObtenerParametrosAsync("CAU-P-999"), Times.Once);
    }

    [Fact]
    public async Task El_controlador_de_reglas_devuelve_los_umbrales_vigentes_con_200()
    {
        var vigentes = new ParametroEvaluacionDto(
            1, RutasNormativas.CambioCarreraOu, 80d, 10d, 70d, 1, DateTimeOffset.UtcNow, "semilla");

        var servicio = new Mock<IReglasService>();
        servicio
            .Setup(s => s.ObtenerParametrosAsync(RutasNormativas.CambioCarreraOu))
            .ReturnsAsync(vigentes);

        var controlador = CrearControladorDeReglas(servicio.Object);

        var resultado = await controlador.ObtenerParametros(RutasNormativas.CambioCarreraOu);

        resultado.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeEquivalentTo(vigentes);
    }

    private static ReglasController CrearControladorDeReglas(IReglasService servicio)
    {
        var contexto = new DefaultHttpContext();
        contexto.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, "admin@uta.edu.ec") }, "Prueba"));

        return new ReglasController(servicio)
        {
            ControllerContext = new ControllerContext { HttpContext = contexto }
        };
    }
}
