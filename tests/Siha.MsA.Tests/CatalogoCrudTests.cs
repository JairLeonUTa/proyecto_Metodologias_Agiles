using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Siha.Contracts;
using Siha.MsA.Api.Contracts.Dtos;
using Siha.MsA.Api.Controllers;
using Siha.MsA.Api.Data;
using Siha.MsA.Api.Domain.Entities;
using Siha.MsA.Api.Services;

namespace Siha.MsA.Tests;

/// <summary>Pruebas simuladas EF InMemory, no sustituyen pruebas contra SQL Server/Gateway.</summary>
public class CatalogoCrudTests
{
    private static async Task<SihaDbContextA> PrepararAsync()
    {
        var db = new SihaDbContextA(new DbContextOptionsBuilder<SihaDbContextA>()
            .UseInMemoryDatabase($"catalogo-crud-{Guid.NewGuid()}").Options);
        var malla = new MallaCurricular
        {
            Carrera = new Carrera { Codigo = " sf ", Nombre = "Software" },
            Version = " v1 ", Anio = 2026, Fuente = "QA"
        };
        var asignatura = new Asignatura { Malla = malla, Codigo = " sf1 ", Nombre = "Plan", Creditos = 4, Horas = 192, Semestre = 1 };
        asignatura.Unidades.Add(new UnidadTematica { Numero = 1, Nombre = "Unidad", Horas = 192 });
        db.Asignaturas.Add(asignatura);
        await db.SaveChangesAsync();
        return db;
    }

    [Theory]
    [InlineData(typeof(CarrerasController))]
    [InlineData(typeof(MallasController))]
    [InlineData(typeof(AsignaturasController))]
    public async Task Delete_es_logico_idempotente_conserva_referencias_y_exige_admin(Type tipo)
    {
        using var db = await PrepararAsync();
        object controller = tipo == typeof(CarrerasController) ? new CarrerasController(db) :
            tipo == typeof(MallasController) ? new MallasController(db) : new AsignaturasController(db, new CatalogoService(db));
        var metodo = tipo.GetMethod("Eliminar");
        metodo.Should().NotBeNull();
        metodo!.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()
            .Should().Contain(a => a.Roles == RolesSistema.Administrador);
        for (var intento = 0; intento < 2; intento++)
            (await (Task<IActionResult>)metodo.Invoke(controller, new object[] { 1 })!).Should().BeOfType<NoContentResult>();
        (await (Task<IActionResult>)metodo.Invoke(controller, new object[] { 999 })!).Should().BeOfType<NotFoundResult>();
        (await db.Carreras.CountAsync()).Should().Be(1);
        (await db.Mallas.CountAsync()).Should().Be(1);
        (await db.Asignaturas.CountAsync()).Should().Be(1);
        (await db.UnidadesTematicas.CountAsync()).Should().Be(1);
        if (tipo == typeof(CarrerasController)) (await db.Carreras.SingleAsync()).Activa.Should().BeFalse();
        if (tipo == typeof(MallasController)) (await db.Mallas.SingleAsync()).Vigente.Should().BeFalse();
        if (tipo == typeof(AsignaturasController)) (await db.Asignaturas.SingleAsync()).Activa.Should().BeFalse();
    }

    [Fact]
    public async Task Codigos_y_versiones_inactivas_permanecen_reservados_normalizados()
    {
        using var db = await PrepararAsync();
        (await db.Carreras.SingleAsync()).Activa = false;
        (await db.Mallas.SingleAsync()).Vigente = false;
        (await db.Asignaturas.SingleAsync()).Activa = false;
        await db.SaveChangesAsync();
        (await new CarrerasController(db).Crear(new("SF", "Duplicada"))).Result.Should().BeOfType<ConflictObjectResult>();
        (await new MallasController(db).Crear(new(1, "V1", 2026, "QA", null))).Result.Should().BeOfType<ConflictObjectResult>();
        var controller = new AsignaturasController(db, new CatalogoService(db));
        (await controller.Crear(new(1, "SF1", "Duplicada", 4, 192, 1))).Result.Should().BeOfType<ConflictObjectResult>();
        var (_, _, resultado) = await new CatalogoService(db).ImportarAsync(new(1, new[]
        {
            new AsignaturaImportItemRequest("SF1", "Duplicada", 4, 192, 1)
        }));
        resultado!.Rechazadas.Should().Be(1);
    }

    [Fact]
    public void Put_mallas_existe_y_no_permite_mover_carrera_en_su_contrato()
    {
        var metodo = typeof(MallasController).GetMethod("Actualizar");
        metodo.Should().NotBeNull();
        var contrato = metodo!.GetParameters().Single(p => p.Name == "request").ParameterType;
        contrato.GetProperty("CarreraId").Should().BeNull();
        contrato.GetProperties().Select(p => p.Name).Should().Contain(new[] { "Version", "Anio", "Fuente", "RutaNormativa", "Vigente" });
    }

    [Fact]
    public async Task Put_mallas_edita_metadatos_sin_mover_carrera_y_conserva_asignaturas()
    {
        using var db = await PrepararAsync();
        var controller = new MallasController(db);
        var resultado = await controller.Actualizar(1, new(" v2 ", 2025, " Resolución ", " cau-p-011 ", false));
        var dto = (MallaDto)resultado.Result.Should().BeOfType<OkObjectResult>().Subject.Value!;
        dto.Version.Should().Be("V2");
        dto.CarreraId.Should().Be(1);
        dto.Anio.Should().Be(2025);
        dto.Fuente.Should().Be("Resolución");
        dto.RutaNormativa.Should().Be("CAU-P-011");
        dto.Vigente.Should().BeFalse();
        dto.TotalAsignaturas.Should().Be(1);
        (await db.Asignaturas.SingleAsync()).MallaId.Should().Be(1);
        (await controller.Actualizar(999, new("V2", 2025, "QA", null, true)))
            .Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Put_mallas_rechaza_version_reservada_e_invalida_sin_mutacion()
    {
        using var db = await PrepararAsync();
        db.Mallas.Add(new MallaCurricular { CarreraId = 1, Version = " v2 ", Fuente = "QA", Anio = 2026, Vigente = false });
        await db.SaveChangesAsync();
        var controller = new MallasController(db);
        (await controller.Actualizar(1, new("V2", 2025, "Cambiada", null, false)))
            .Result.Should().BeOfType<ConflictObjectResult>();
        (await controller.Actualizar(1, new("V3", 1900, "Cambiada", null, false)))
            .Result.Should().BeOfType<BadRequestObjectResult>();
        var malla = await db.Mallas.FindAsync(1);
        malla!.Version.Should().Be(" v1 ");
        malla.Fuente.Should().Be("QA");
        malla.Vigente.Should().BeTrue();
        db.ChangeTracker.HasChanges().Should().BeFalse();
    }

    [Fact]
    public async Task Asignatura_inactiva_sigue_visible_y_edicion_no_la_reactiva()
    {
        using var db = await PrepararAsync();
        var controller = new AsignaturasController(db, new CatalogoService(db));
        await controller.Eliminar(1);
        var listado = (await controller.Listar(1, " SF1 ")).Result.Should().BeOfType<OkObjectResult>().Subject;
        ((IEnumerable<AsignaturaDto>)listado.Value!).Should().ContainSingle().Which.Activa.Should().BeFalse();
        var lectura = (await controller.Obtener(1)).Result.Should().BeOfType<OkObjectResult>().Subject;
        ((AsignaturaDto)lectura.Value!).Activa.Should().BeFalse();
        var editada = (await controller.Actualizar(1, new("Corrección histórica", 4, 192, 1)))
            .Result.Should().BeOfType<OkObjectResult>().Subject;
        ((AsignaturaDto)editada.Value!).Activa.Should().BeFalse();
    }
}
