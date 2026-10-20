using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Siha.MsA.Api.Contracts.Dtos;
using Siha.MsA.Api.Controllers;
using Siha.MsA.Api.Data;
using Siha.MsA.Api.Security;
using Siha.MsA.Api.Services;

namespace Siha.MsA.Tests;

// Pruebas simuladas: EF InMemory, no verifican SQL Server ni el Gateway.
[Trait("Entorno", "Simulada-InMemory")]
public class ReglasServiceTests
{
    private static SihaDbContextA NuevaDb() => new(new DbContextOptionsBuilder<SihaDbContextA>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CrearReglaRequest Regla() => new("RN-02", "CAU-P-122", "EXTERNO",
        "Al menos 80 % de resultados de aprendizaje.", "ra>=80", "1.0",
        "CAU-P-122, Art. 20", true, new DateOnly(2026, 1, 1), null);

    [Fact]
    public async Task ControladorPutExpone200400404YExigeAdministrador()
    {
        using var db = NuevaDb();
        var service = new ReglasService(db);
        var controller = new ReglasController(service);
        var original = (await service.CrearReglaAsync(Regla())).Regla!;
        Assert.IsType<NotFoundObjectResult>((await controller.Actualizar(999, Regla())).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.Actualizar(original.Id, Regla())).Result);
        Assert.IsType<OkObjectResult>((await controller.Actualizar(original.Id,
            Regla() with { Version = "2.0", VigenteDesde = new(2026, 2, 1) })).Result);
        var metodo = typeof(ReglasController).GetMethod(nameof(ReglasController.Actualizar))!;
        Assert.Equal(Siha.Contracts.RolesSistema.Administrador,
            Assert.Single(metodo.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>()).Roles);
        Assert.Equal("{id:int}", Assert.Single(metodo.GetCustomAttributes(typeof(HttpPutAttribute), false)
            .Cast<HttpPutAttribute>()).Template);
    }

    [Fact]
    public async Task PostNoPermiteOtraVersionDeUnaIdentidadExistente()
    {
        using var db = NuevaDb();
        var service = new ReglasService(db);
        await service.CrearReglaAsync(Regla());
        var resultado = await service.CrearReglaAsync(Regla() with { Version = "2.0" });
        Assert.False(resultado.Ok);
        Assert.Single(db.Reglas);
    }

    [Fact]
    public async Task PutCreaFilaCierraVigenciaYConservaHistorialInclusoConVersionFutura()
    {
        using var db = NuevaDb();
        var service = new ReglasService(db);
        var original = (await service.CrearReglaAsync(Regla())).Regla!;
        var inicio = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10);
        var resultado = await service.ActualizarReglaAsync(original.Id,
            Regla() with { Version = "2.0", Descripcion = "Nueva redacción", VigenteDesde = inicio });
        Assert.True(resultado.Ok);
        Assert.NotEqual(original.Id, resultado.Regla!.Id);
        var historial = await service.ListarHistorialReglasAsync(null, null);
        Assert.Equal(2, historial.Count);
        var anterior = Assert.Single(historial.Where(r => r.Id == original.Id));
        Assert.Equal(inicio.AddDays(-1), anterior.VigenteHasta);
        Assert.Equal(original.Expresion, anterior.Expresion);
        Assert.Equal(original.Descripcion, anterior.Descripcion);
        Assert.Equal(original.Id, Assert.Single(await service.ListarReglasAsync(null, null)).Id);
        Assert.False((await service.ActualizarReglaAsync(original.Id,
            Regla() with { Version = "3.0", VigenteDesde = inicio.AddDays(1) })).Ok);
    }

    [Fact]
    public async Task PutVigenteHoyExcluyeHistoricaYNoTocaRaAlActualizarHeuristica()
    {
        using var db = NuevaDb();
        var service = new ReglasService(db);
        var original = (await service.CrearReglaAsync(Regla())).Regla!;
        var nueva = await service.ActualizarReglaAsync(original.Id,
            Regla() with { Version = "2.0", VigenteDesde = DateOnly.FromDateTime(DateTime.UtcNow) });
        Assert.True(nueva.Ok);
        Assert.Equal(nueva.Regla!.Id, Assert.Single(await service.ListarReglasAsync("CAU-P-122", "EXTERNO")).Id);
        await service.ActualizarParametrosAsync("CAU-P-122", new(60, 15, 90), "admin");
        Assert.All(db.Reglas, r => Assert.Equal("ra>=80", r.Expresion));
    }

    [Fact]
    public async Task PutRechazaIdentidadVersionYFechasInvalidasSinMutar()
    {
        using var db = NuevaDb();
        var service = new ReglasService(db);
        var original = (await service.CrearReglaAsync(Regla())).Regla!;
        var request = Regla() with { Version = "2.0", VigenteDesde = new(2026, 2, 1) };
        var invalidos = new[]
        {
            request with { Codigo = "RN-99" }, request with { RutaNormativa = "CAU-P-011" },
            request with { EsNormativa = false }, request with { TipoTramite = "INTERNO" },
            request with { Version = "1.0" }, request with { Version = "" },
            request with { VigenteDesde = original.VigenteDesde },
            request with { VigenteHasta = request.VigenteDesde.AddDays(-1) }
        };
        foreach (var invalido in invalidos)
        {
            Assert.False((await service.ActualizarReglaAsync(original.Id, invalido)).Ok);
            Assert.Single(db.Reglas);
            Assert.Null((await db.Reglas.FindAsync(original.Id))!.VigenteHasta);
        }
        var inexistente = await service.ActualizarReglaAsync(9999, request);
        Assert.False(inexistente.Ok);
        Assert.Null(inexistente.Error);
    }

    [Fact]
    public async Task ListarVigentesExcluyeInactivasVencidasYFuturas()
    {
        using var db = NuevaDb();
        var service = new ReglasService(db);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        await service.CrearReglaAsync(Regla() with { VigenteDesde = hoy.AddDays(1) });
        await service.CrearReglaAsync(Regla() with { Codigo = "RN-09", VigenteHasta = hoy.AddDays(-1) });
        var inactiva = (await service.CrearReglaAsync(Regla() with { Codigo = "RN-10" })).Regla!;
        (await db.Reglas.FindAsync(inactiva.Id))!.Activa = false;
        await db.SaveChangesAsync();
        Assert.Empty(await service.ListarReglasAsync(null, null));
        Assert.Equal(3, (await service.ListarHistorialReglasAsync(null, null)).Count);
    }

    [Fact]
    public async Task ActualizarParametrosConservaFilaAnteriorYDevuelveUltimaVersion()
    {
        using var db = NuevaDb();
        var service = new ReglasService(db);
        var anterior = await service.ActualizarParametrosAsync("CAU-P-122", new(80, 15, 75), "admin1");
        var siguiente = await service.ActualizarParametrosAsync("CAU-P-122", new(85, 10, 70), "admin2");
        Assert.True(siguiente.Ok);
        Assert.Equal(2, await db.ParametrosEvaluacion.CountAsync());
        Assert.Equal(80, (await db.ParametrosEvaluacion.FindAsync(anterior.Parametros!.Id))!.Theta);
        Assert.NotEqual(anterior.Parametros.Id, siguiente.Parametros!.Id);
        Assert.Equal(2, siguiente.Parametros.Version);
        Assert.Equal(siguiente.Parametros, await service.ObtenerParametrosAsync("CAU-P-122"));
        var invalido = await service.ActualizarParametrosAsync("CAU-P-122", new(10, 20, 75), "admin");
        Assert.False(invalido.Ok);
        Assert.Equal(2, await db.ParametrosEvaluacion.CountAsync());
    }

    [Fact]
    public async Task SemillaNuevaUsaIdsOficialesYDefaultsSinConfundirRaConTheta()
    {
        using var db = NuevaDb();
        await Sembrar(db);
        Assert.Equal(new[] { "RN-02", "RN-09", "RN-10", "RN-13", "RN-14" },
            await db.Reglas.OrderBy(r => r.Codigo).Select(r => r.Codigo).ToArrayAsync());
        Assert.Equal(new[] { "CAU-P-011", "CAU-P-122" }, await db.ParametrosEvaluacion
            .OrderBy(p => p.RutaNormativa).Select(p => p.RutaNormativa).ToArrayAsync());
        Assert.All(db.ParametrosEvaluacion, p =>
        {
            Assert.Equal(80, p.Theta);
            Assert.Equal(15, p.Delta);
            Assert.Equal(75, p.Tau);
        });
        var ra = await db.Reglas.SingleAsync(r => r.Codigo == "RN-02");
        Assert.True(ra.EsNormativa);
        Assert.Equal("ra>=80", ra.Expresion);
        Assert.All(db.Reglas.Where(r => r.Codigo == "RN-13" || r.Codigo == "RN-14"),
            r => Assert.False(r.EsNormativa));
        Assert.Equal("cobertura>=tau", (await db.Reglas.SingleAsync(r => r.Codigo == "RN-14")).Expresion);
    }

    [Fact]
    public async Task SemillaNoRenumeraNiRecalibraInstalacionLegada()
    {
        using var db = NuevaDb();
        var service = new ReglasService(db);
        await service.CrearReglaAsync(Regla() with { Codigo = "RN-05", Expresion = "theta>=80" });
        await service.ActualizarParametrosAsync("CAU-P-122", new(80, 15, 65), "legado");
        await Sembrar(db);
        Assert.Equal("RN-05", Assert.Single(db.Reglas).Codigo);
        Assert.Equal("theta>=80", Assert.Single(db.Reglas).Expresion);
        Assert.Equal(65, Assert.Single(db.ParametrosEvaluacion).Tau);
    }

    [Fact]
    public async Task SemillaRepetidaNoSobrescribeConfiguracionExistente()
    {
        using var db = NuevaDb();
        await Sembrar(db);
        var parametro = await db.ParametrosEvaluacion.FirstAsync();
        parametro.Tau = 42;
        var regla = await db.Reglas.FirstAsync();
        regla.Descripcion = "Configuración local";
        await db.SaveChangesAsync();
        await Sembrar(db);
        Assert.Equal(2, await db.ParametrosEvaluacion.CountAsync());
        Assert.Equal(5, await db.Reglas.CountAsync());
        Assert.Equal(42, parametro.Tau);
        Assert.Equal("Configuración local", regla.Descripcion);
    }

    [Fact]
    public async Task SemillaNoMarcaCompletosLosPlanesConHorasDeUnidadesFaltantes()
    {
        using var db = NuevaDb();
        await Sembrar(db);
        var asignaturas = await db.Asignaturas.Include(a => a.Unidades).ToListAsync();
        Assert.NotEmpty(asignaturas);
        Assert.All(asignaturas, a =>
        {
            Assert.True(a.Unidades.Sum(u => u.Horas) < a.Horas);
            Assert.False(a.PlanCompleto);
        });
    }

    private static Task Sembrar(SihaDbContextA db) => DbSeederA.SembrarAsync(db,
        new BCryptPasswordHasher(), "admin@uta.edu.ec", "no-usada", NullLogger.Instance);
}
