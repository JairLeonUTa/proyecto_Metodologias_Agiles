using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Siha.MsA.Api.Contracts;
using Siha.MsA.Api.Contracts.Dtos;
using Siha.MsA.Api.Controllers;
using Siha.MsA.Api.Data;
using Siha.MsA.Api.Domain.Entities;
using Siha.MsA.Api.Services;

namespace Siha.MsA.Tests;

/// <summary>Pruebas simuladas con EF InMemory; no verifican restricciones de SQL Server.</summary>
public class CatalogoValidacionTests
{
    private static async Task<SihaDbContextA> PrepararAsync()
    {
        var db = new SihaDbContextA(new DbContextOptionsBuilder<SihaDbContextA>()
            .UseInMemoryDatabase($"catalogo-validacion-{Guid.NewGuid()}").Options);
        db.Mallas.Add(new MallaCurricular
        {
            Carrera = new Carrera { Codigo = "SF", Nombre = "Software" },
            Version = "2026", Anio = 2026, Fuente = "QA"
        });
        await db.SaveChangesAsync();
        return db;
    }

    [Theory]
    [InlineData(0, 192)]
    [InlineData(4, 0)]
    public async Task Create_edit_import_rechazan_campos_invalidos_sin_modificar(int creditos, int horas)
    {
        using var db = await PrepararAsync();
        var service = new CatalogoService(db);
        var controller = new AsignaturasController(db, service);
        var creado = await controller.Crear(new(1, "SF1", "Plan", creditos, horas, 1));
        creado.Result.Should().BeOfType<BadRequestObjectResult>();
        var (_, _, resultado) = await service.ImportarAsync(new(1, new[]
        {
            new AsignaturaImportItemRequest("SF1", "Plan", creditos, horas, 1)
        }));
        resultado!.Rechazadas.Should().Be(1);
        (await db.Asignaturas.CountAsync()).Should().Be(0);
        var existente = new Asignatura { MallaId = 1, Codigo = "SF2", Nombre = "Original", Creditos = 4, Horas = 192, Semestre = 1 };
        db.Asignaturas.Add(existente);
        await db.SaveChangesAsync();
        (await controller.Actualizar(existente.Id, new("Cambio", creditos, horas, 1)))
            .Result.Should().BeOfType<BadRequestObjectResult>();
        existente.Creditos.Should().Be(4);
        existente.Horas.Should().Be(192);
        db.ChangeTracker.HasChanges().Should().BeFalse();
    }

    [Fact]
    public async Task Create_edit_import_rechazan_unidades_duplicadas_sin_modificar()
    {
        using var db = await PrepararAsync();
        var service = new CatalogoService(db);
        var controller = new AsignaturasController(db, service);
        var duplicadas = new[] { new UnidadTematicaRequest(1, "Uno", 96), new UnidadTematicaRequest(1, "Dos", 96) };
        (await controller.Crear(new(1, "SF1", "Plan", 4, 192, 1, duplicadas)))
            .Result.Should().BeOfType<BadRequestObjectResult>();
        var (_, _, resultado) = await service.ImportarAsync(new(1, new[]
        {
            new AsignaturaImportItemRequest("SF1", "Plan", 4, 192, 1, duplicadas)
        }));
        resultado!.Rechazadas.Should().Be(1);
        var asignatura = new Asignatura { MallaId = 1, Codigo = "SF2", Nombre = "Original", Creditos = 4, Horas = 192, Semestre = 1 };
        asignatura.Unidades.Add(new UnidadTematica { Numero = 1, Nombre = "Original", Horas = 192 });
        db.Asignaturas.Add(asignatura);
        await db.SaveChangesAsync();
        (await controller.Actualizar(asignatura.Id, new("Modificada", 4, 192, 1, duplicadas)))
            .Result.Should().BeOfType<BadRequestObjectResult>();
        asignatura.Nombre.Should().Be("Original");
        asignatura.Unidades.Should().ContainSingle().Which.Nombre.Should().Be("Original");
        db.ChangeTracker.HasChanges().Should().BeFalse();
    }

    [Fact]
    public async Task Editar_horas_sin_unidades_recalcula_completitud_y_conserva_unidades()
    {
        using var db = await PrepararAsync();
        var asignatura = new Asignatura { MallaId = 1, Codigo = "SF1", Nombre = "Plan", Creditos = 4, Horas = 192, Semestre = 1, PlanCompleto = true };
        asignatura.Unidades.Add(new UnidadTematica { Numero = 1, Nombre = "Unidad", Horas = 192 });
        db.Asignaturas.Add(asignatura);
        await db.SaveChangesAsync();
        var controller = new AsignaturasController(db, new CatalogoService(db));
        await controller.Actualizar(asignatura.Id, new("Plan", 3, 144, 1));
        asignatura.PlanCompleto.Should().BeFalse();
        asignatura.Unidades.Should().ContainSingle().Which.Horas.Should().Be(192);
    }

    [Fact]
    public async Task Import_normaliza_codigos_y_rechaza_duplicado_historico_y_del_lote()
    {
        using var db = await PrepararAsync();
        db.Asignaturas.Add(new Asignatura { MallaId = 1, Codigo = " sf0 ", Nombre = "Histórico" });
        await db.SaveChangesAsync();
        var (_, _, resultado) = await new CatalogoService(db).ImportarAsync(new(1, new[]
        {
            new AsignaturaImportItemRequest(" sf1 ", "Plan", 4, 192, 1),
            new AsignaturaImportItemRequest("SF1", "Duplicado", 4, 192, 1),
            new AsignaturaImportItemRequest("SF0", "Histórico", 4, 192, 1)
        }));
        resultado!.Importadas.Should().Be(1);
        resultado.Rechazadas.Should().Be(2);
        (await db.Asignaturas.SingleAsync(a => a.Nombre == "Plan")).Codigo.Should().Be("SF1");
    }

    [Theory]
    [InlineData(100)]
    [InlineData(200)]
    public void Lectura_no_expone_completo_un_historico_con_suma_incorrecta(int horasUnidad)
    {
        var asignatura = new Asignatura { Codigo = "SF1", Nombre = "Plan", Creditos = 4, Horas = 192, Semestre = 1, PlanCompleto = true };
        asignatura.Unidades.Add(new UnidadTematica { Numero = 1, Nombre = "Unidad", Horas = horasUnidad });
        Mapeos.AAsignaturaDto(asignatura).PlanCompleto.Should().BeFalse();
        asignatura.PlanCompleto.Should().BeTrue("la lectura no debe mutar ni corregir silenciosamente la base");
    }

    [Theory]
    [InlineData(0, 192)]
    [InlineData(51, 192)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public async Task Todas_las_escrituras_rechazan_unidades_invalidas(int numero, int horas)
    {
        using var db = await PrepararAsync();
        var unidades = new[] { new UnidadTematicaRequest(numero, "Unidad", horas) };
        var service = new CatalogoService(db);
        var controller = new AsignaturasController(db, service);
        (await controller.Crear(new(1, "SF1", "Plan", 4, 192, 1, unidades)))
            .Result.Should().BeOfType<BadRequestObjectResult>();
        var (_, _, resultado) = await service.ImportarAsync(new(1, new[]
        {
            new AsignaturaImportItemRequest("SF1", "Plan", 4, 192, 1, unidades)
        }));
        resultado!.Rechazadas.Should().Be(1);
        db.Asignaturas.Add(new Asignatura { MallaId = 1, Codigo = "SF2", Nombre = "Original", Creditos = 4, Horas = 192, Semestre = 1 });
        await db.SaveChangesAsync();
        (await controller.Actualizar(1, new("Cambio", 4, 192, 1, unidades)))
            .Result.Should().BeOfType<BadRequestObjectResult>();
        db.ChangeTracker.HasChanges().Should().BeFalse();
    }

    [Fact]
    public async Task Alta_y_edicion_advierten_plan_pendiente_y_aceptan_contenidos_vacios()
    {
        using var db = await PrepararAsync();
        var controller = new AsignaturasController(db, new CatalogoService(db));
        var alta = (await controller.Crear(new(1, "SF1", "Plan", 4, 192, 1)))
            .Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var dto = (AsignaturaDto)alta.Value!;
        dto.PlanCompleto.Should().BeFalse();
        dto.Advertencias.Should().Contain(a => a.Contains("no tiene unidades"));
        var edicion = (await controller.Actualizar(dto.Id, new("Plan", 4, 192, 1,
            new[] { new UnidadTematicaRequest(1, "Unidad", 192, "") })))
            .Result.Should().BeOfType<OkObjectResult>().Subject;
        ((AsignaturaDto)edicion.Value!).PlanCompleto.Should().BeTrue();
        ((AsignaturaDto)edicion.Value!).Advertencias.Should().BeEmpty();
    }

    [Fact]
    public async Task Import_rechaza_entrada_y_unidad_null_sin_insertar()
    {
        using var db = await PrepararAsync();
        var (_, _, resultado) = await new CatalogoService(db).ImportarAsync(new(1, new AsignaturaImportItemRequest[]
        {
            null!,
            new AsignaturaImportItemRequest("SF1", "Plan", 4, 192, 1, new UnidadTematicaRequest[] { null! })
        }));
        resultado!.Rechazadas.Should().Be(2);
        (await db.Asignaturas.CountAsync()).Should().Be(0);
    }
}
