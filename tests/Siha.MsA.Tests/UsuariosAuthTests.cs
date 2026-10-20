using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Siha.Contracts;
using Siha.MsA.Api.Contracts.Dtos;
using Siha.MsA.Api.Controllers;
using Siha.MsA.Api.Data;
using Siha.MsA.Api.Domain.Entities;
using Siha.MsA.Api.Security;

namespace Siha.MsA.Tests;

// Unitarias: llamadas directas, sin pipeline HTTP ni SQL Server real.
public class UsuariosAuthTests
{
    private const string Password = "UnitTestOnly123!";
    private static readonly BCryptPasswordHasher Hasher = new();
    private static readonly Lazy<string> Hash = new(() => Hasher.Hash(Password));

    [Fact]
    public void Login_permite_password_corto_no_vacio_en_metadata_de_binding()
    {
        // Los records MVC validan los atributos de los parametros del constructor.
        var parametro = typeof(LoginRequest).GetConstructors().Single()
            .GetParameters().Single(p => p.Name == nameof(LoginRequest.Password));
        var atributos = parametro.GetCustomAttributes<ValidationAttribute>().ToArray();
        atributos.Should().Contain(a => a is RequiredAttribute);
        atributos.Should().OnlyContain(a => a.IsValid("x"));
        atributos.Should().Contain(a => !a.IsValid(""));
    }

    [Theory]
    [InlineData("docente@uta.edu.ec")]
    [InlineData("  DOCENTE@UTA.EDU.EC  ")]
    public async Task Login_activo_emite_JWT_con_rol_y_actualiza_acceso(string correo)
    {
        using var db = await CrearDb();
        var usuario = await AgregarUsuario(db);
        var jwt = new JwtTokenService(Options.Create(new JwtSettings
        {
            Key = "clave-de-pruebas-para-firma-JWT-con-256-bits-minimo-ok"
        }));

        var resultado = await new AuthController(db, Hasher, jwt)
            .Login(new LoginRequest(correo, Password));

        var respuesta = Assert.IsType<LoginResponse>(Assert.IsType<OkObjectResult>(resultado.Result).Value);
        respuesta.Usuario.Rol.Should().Be(RolesSistema.Docente);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(respuesta.Token);
        token.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == RolesSistema.Docente);
        usuario.UltimoAccesoUtc.Should().NotBeNull();
        SinPassword(respuesta);
    }

    [Theory]
    [InlineData("x", true, true)]
    [InlineData("incorrecta123", true, true)]
    [InlineData(Password, false, true)]
    [InlineData(Password, true, false)]
    public async Task Login_incorrecto_inactivo_o_inexistente_devuelve_401_sin_token(
        string password, bool activo, bool existe)
    {
        using var db = await CrearDb();
        var usuario = existe ? await AgregarUsuario(db, activo) : null;
        var jwt = new Mock<IJwtTokenService>(MockBehavior.Strict);

        var resultado = await new AuthController(db, Hasher, jwt.Object)
            .Login(new LoginRequest("docente@uta.edu.ec", password));

        Assert.IsType<UnauthorizedObjectResult>(resultado.Result);
        if (existe)
        {
            Assert.Null(usuario!.UltimoAccesoUtc);
        }
        jwt.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(RolesSistema.Administrador, true)]
    [InlineData(RolesSistema.Docente, false)]
    [InlineData(RolesSistema.Operador, true)]
    public async Task Crear_persiste_BCrypt_rol_estado_y_no_serializa_hash(string rol, bool activo)
    {
        using var db = await CrearDb();
        var resultado = await new UsuariosController(db, Hasher).Crear(
            new CrearUsuarioRequest("  NUEVO@UTA.EDU.EC  ", "  Nueva Cuenta  ", Password, rol, activo));

        var respuesta = Assert.IsType<UsuarioDto>(Assert.IsType<CreatedAtActionResult>(resultado.Result).Value);
        var usuario = await db.Usuarios.SingleAsync();
        usuario.CorreoInstitucional.Should().Be("nuevo@uta.edu.ec");
        usuario.NombreCompleto.Should().Be("Nueva Cuenta");
        usuario.Activo.Should().Be(activo);
        usuario.Rol!.Nombre.Should().Be(rol);
        usuario.PasswordHash.Should().StartWith("$2");
        Hasher.Verify(Password, usuario.PasswordHash).Should().BeTrue();
        respuesta.Rol.Should().Be(rol);
        respuesta.Activo.Should().Be(activo);
        SinPassword(respuesta);
        SinPassword(Assert.IsType<OkObjectResult>((await new UsuariosController(db, Hasher).Obtener(usuario.Id)).Result).Value!);
        SinPassword(Assert.IsType<OkObjectResult>((await new UsuariosController(db, Hasher).Listar(null)).Result).Value!);
    }

    [Fact]
    public async Task Crear_duplicado_normalizado_devuelve_409_antes_de_hash()
    {
        using var db = await CrearDb();
        await AgregarUsuario(db);
        var hasher = new Mock<IPasswordHasher>(MockBehavior.Strict);

        var resultado = await new UsuariosController(db, hasher.Object).Crear(
            new CrearUsuarioRequest("  DOCENTE@UTA.EDU.EC  ", "Duplicado", Password, RolesSistema.Docente));

        Assert.IsType<ConflictObjectResult>(resultado.Result);
        (await db.Usuarios.CountAsync()).Should().Be(1);
        hasher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Actualizar_y_cambiar_estado_conservan_hash_y_correo()
    {
        using var db = await CrearDb();
        var usuario = await AgregarUsuario(db);
        var controlador = new UsuariosController(db, Hasher);

        var actualizado = await controlador.Actualizar(usuario.Id,
            new ActualizarUsuarioRequest("  Nombre Actualizado  ", RolesSistema.Operador, false));
        var dto = Assert.IsType<UsuarioDto>(Assert.IsType<OkObjectResult>(actualizado.Result).Value);
        dto.Rol.Should().Be(RolesSistema.Operador);
        dto.Activo.Should().BeFalse();
        dto.NombreCompleto.Should().Be("Nombre Actualizado");
        SinPassword(dto);
        var estado = await controlador.CambiarEstado(usuario.Id, new CambiarEstadoUsuarioRequest(true));
        Assert.IsType<UsuarioDto>(Assert.IsType<OkObjectResult>(estado.Result).Value).Activo.Should().BeTrue();
        usuario.PasswordHash.Should().Be(Hash.Value);
        usuario.CorreoInstitucional.Should().Be("docente@uta.edu.ec");
    }

    [Theory]
    [InlineData(2601)]
    [InlineData(2627)]
    public async Task Crear_traduce_unique_SQL_en_carrera_a_409(int numero)
    {
        using var db = await CrearDb();
        db.ErrorGuardar = new DbUpdateException("fallo", ErrorSql(numero));
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Hash(Password)).Returns(Hash.Value);

        var resultado = await new UsuariosController(db, hasher.Object).Crear(
            new CrearUsuarioRequest("nuevo@uta.edu.ec", "Nuevo Usuario", Password, RolesSistema.Docente));

        var conflicto = Assert.IsType<ConflictObjectResult>(resultado.Result);
        JsonSerializer.Serialize(conflicto.Value).Should().NotContain("fallo");
    }

    [Theory]
    [InlineData(547)]
    [InlineData(-2)]
    [InlineData(0)]
    public async Task Crear_no_oculta_otros_errores_de_persistencia(int numero)
    {
        using var db = await CrearDb();
        var error = new DbUpdateException("fallo", numero == 0 ? new Exception("otro") : ErrorSql(numero));
        db.ErrorGuardar = error;
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Hash(Password)).Returns(Hash.Value);

        var lanzado = await Assert.ThrowsAsync<DbUpdateException>(() => new UsuariosController(db, hasher.Object).Crear(
            new CrearUsuarioRequest("nuevo@uta.edu.ec", "Nuevo Usuario", Password, RolesSistema.Docente)));

        lanzado.Should().BeSameAs(error);
    }

    private static void SinPassword(object respuesta)
    {
        var json = JsonSerializer.Serialize(respuesta);
        json.ToLowerInvariant().Should().NotContain("password");
        json.Should().NotContain(Hash.Value).And.NotContain(Password);
    }

    private static async Task<DbConFallo> CrearDb()
    {
        var db = new DbConFallo(new DbContextOptionsBuilder<SihaDbContextA>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Roles.AddRange(RolesSistema.Todos.Select((nombre, i) => new Rol { Id = i + 1, Nombre = nombre }));
        await db.SaveChangesAsync();
        return db;
    }

    private static async Task<Usuario> AgregarUsuario(SihaDbContextA db, bool activo = true)
    {
        var rol = await db.Roles.SingleAsync(r => r.Nombre == RolesSistema.Docente);
        var usuario = new Usuario
        {
            CorreoInstitucional = "docente@uta.edu.ec", NombreCompleto = "Cuenta Docente",
            PasswordHash = Hash.Value, RolId = rol.Id, Rol = rol, Activo = activo
        };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        return usuario;
    }

    private sealed class DbConFallo(DbContextOptions<SihaDbContextA> options) : SihaDbContextA(options)
    {
        public DbUpdateException? ErrorGuardar { get; set; }
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            ErrorGuardar is { } error ? Task.FromException<int>(error) : base.SaveChangesAsync(cancellationToken);
    }

    // SqlClient no expone constructores publicos de sus errores. Solo fabrica el fallo;
    // no simula que InMemory implemente indices unicos ni concurrencia SQL.
    private static SqlException ErrorSql(int numero)
    {
        const BindingFlags internos = BindingFlags.Instance | BindingFlags.NonPublic;
        var constructor = typeof(SqlError).GetConstructors(internos).First();
        var argumentos = constructor.GetParameters().Select((p, i) => i == 0 ? (object)numero
            : p.ParameterType == typeof(string) ? "error de prueba"
            : p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null).ToArray();
        var error = (SqlError)constructor.Invoke(argumentos);
        var errores = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true)!;
        typeof(SqlErrorCollection).GetMethod("Add", internos)!.Invoke(errores, new object[] { error });
        return (SqlException)typeof(SqlException).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(m => m.Name == "CreateException" && m.GetParameters().Length == 2)
            .Invoke(null, new object[] { errores, "test" })!;
    }
}
