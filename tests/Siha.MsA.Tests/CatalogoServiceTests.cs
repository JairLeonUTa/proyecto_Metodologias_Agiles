using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Siha.MsA.Api.Data;
using Siha.MsA.Api.Domain.Entities;
using Siha.MsA.Api.Services;

namespace Siha.MsA.Tests;

public class CatalogoServiceTests
{
    private static SihaDbContextA CrearContexto()
    {
        var options = new DbContextOptionsBuilder<SihaDbContextA>()
            .UseInMemoryDatabase($"siha-tests-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new SihaDbContextA(options);
    }

    private static async Task<(SihaDbContextA Db, int MallaId)> PrepararAsync()
    {
        var db = CrearContexto();
        var carrera = new Carrera { Codigo = "SW", Nombre = "Ingeniería de Software" };
        db.Carreras.Add(carrera);
        var malla = new MallaCurricular { Carrera = carrera, Version = "2024-A", Anio = 2024, Fuente = "FISEI" };
        db.Mallas.Add(malla);
        await db.SaveChangesAsync();
        return (db, malla.Id);
    }

    [Fact]
    public async Task Rechaza_codigos_duplicados_en_la_misma_malla()
    {
        var (db, mallaId) = await PrepararAsync();
        var servicio = new CatalogoService(db);

        var request = new Siha.MsA.Api.Contracts.Dtos.ImportarCatalogoRequest(mallaId, new List<Siha.MsA.Api.Contracts.Dtos.AsignaturaImportItemRequest>
        {
            new("SW101", "Programación I", 4, 192, 1, new List<Siha.MsA.Api.Contracts.Dtos.UnidadTematicaRequest>
            {
                new(1, "Unidad 1", 96, "Contenidos"),
                new(2, "Unidad 2", 96, "Contenidos")
            }),
            new("SW101", "Programación I (duplicada)", 4, 192, 1, null)
        });

        var (ok, error, resultado) = await servicio.ImportarAsync(request);

        ok.Should().BeTrue();
        error.Should().BeNull();
        resultado!.Importadas.Should().Be(1);
        resultado.Rechazadas.Should().Be(1);
        resultado.Errores.Should().Contain(e => e.Contains("duplicado"));
    }

    [Fact]
    public async Task Marca_pendiente_el_plan_que_no_cumple_48h_por_credito()
    {
        var (db, mallaId) = await PrepararAsync();
        var servicio = new CatalogoService(db);

        var request = new Siha.MsA.Api.Contracts.Dtos.ImportarCatalogoRequest(mallaId, new List<Siha.MsA.Api.Contracts.Dtos.AsignaturaImportItemRequest>
        {
            new("SW150", "Base de Datos", 4, 100, 2, new List<Siha.MsA.Api.Contracts.Dtos.UnidadTematicaRequest>
            {
                new(1, "Modelo relacional", 50, "Contenidos"),
                new(2, "SQL", 50, "Contenidos")
            })
        });

        var (ok, _, resultado) = await servicio.ImportarAsync(request);

        ok.Should().BeTrue();
        resultado!.Errores.Should().Contain(e => e.Contains("48 h/crédito"));
        var importada = await db.Asignaturas.SingleAsync();
        importada.PlanCompleto.Should().BeFalse();
    }

    [Fact]
    public async Task Importa_plan_completo_con_equivalencia_de_48_horas()
    {
        var (db, mallaId) = await PrepararAsync();
        var servicio = new CatalogoService(db);

        var request = new Siha.MsA.Api.Contracts.Dtos.ImportarCatalogoRequest(mallaId, new List<Siha.MsA.Api.Contracts.Dtos.AsignaturaImportItemRequest>
        {
            new("SW200", "Estructuras de Datos", 4, 192, 2, new List<Siha.MsA.Api.Contracts.Dtos.UnidadTematicaRequest>
            {
                new(1, "Listas", 64, "Contenidos"),
                new(2, "Árboles", 64, "Contenidos"),
                new(3, "Grafos", 64, "Contenidos")
            })
        });

        var (ok, _, resultado) = await servicio.ImportarAsync(request);

        ok.Should().BeTrue();
        resultado!.Importadas.Should().Be(1);
        resultado.Rechazadas.Should().Be(0);
        var importada = await db.Asignaturas.Include(a => a.Unidades).SingleAsync();
        importada.PlanCompleto.Should().BeTrue();
        importada.Unidades.Should().HaveCount(3);
    }

    [Fact]
    public async Task Falla_si_la_malla_no_existe()
    {
        var db = CrearContexto();
        var servicio = new CatalogoService(db);

        var request = new Siha.MsA.Api.Contracts.Dtos.ImportarCatalogoRequest(999, new List<Siha.MsA.Api.Contracts.Dtos.AsignaturaImportItemRequest>
        {
            new("SW999", "Inexistente", 4, 192, 1, null)
        });

        var (ok, error, _) = await servicio.ImportarAsync(request);

        ok.Should().BeFalse();
        error.Should().Contain("no existe");
    }
}
