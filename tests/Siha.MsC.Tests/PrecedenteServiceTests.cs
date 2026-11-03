using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Siha.MsC.Api.Contracts.Dtos;
using Siha.MsC.Api.Data;
using Siha.MsC.Api.Domain.Entities;
using Siha.MsC.Api.Services;

namespace Siha.MsC.Tests;

public class PrecedenteServiceTests
{
    private static SihaDbContextC CrearDb()
        => new(new DbContextOptionsBuilder<SihaDbContextC>()
            .UseInMemoryDatabase($"siha-msc-precedentes-{Guid.NewGuid():N}")
            .Options);

    [Fact]
    public async Task BuscarAsync_coincidencia_exacta_devuelve_precedente()
    {
        await using var db = CrearDb();

        var precedente = new Precedente
        {
            Resolucion = "RES-FISEI-2025-031",
            AsignaturasOrigenJson = """[{"Codigo":"SW101","Nombre":"Fundamentos de Programación"},{"Codigo":"SW102","Nombre":"Programación II"}]""",
            AsignaturaDestino = "TI101",
            InstitucionOrigen = "UNIVERSIDAD TECNICA DE AMBATO",
            Fecha = new DateOnly(2025, 6, 15)
        };
        db.Precedentes.Add(precedente);
        await db.SaveChangesAsync();

        var service = new PrecedenteService(db);

        var request = new BuscarPrecedenteRequest(
            new[] { "SW101", "SW102" },
            "TI101",
            "Universidad Tecnica de Ambato");

        var resultados = await service.BuscarAsync(request);

        resultados.Should().HaveCount(1);
        resultados.First().Resolucion.Should().Be("RES-FISEI-2025-031");
        resultados.First().AsignaturaDestino.Should().Be("TI101");
        resultados.First().InstitucionOrigen.Should().Be("UNIVERSIDAD TECNICA DE AMBATO");
        resultados.First().AsignaturasOrigen.Should().HaveCount(2);
    }

    [Fact]
    public async Task BuscarAsync_sin_coincidencia_devuelve_lista_vacia()
    {
        await using var db = CrearDb();

        var precedente = new Precedente
        {
            Resolucion = "RES-FISEI-2025-031",
            AsignaturasOrigenJson = """[{"Codigo":"SW101","Nombre":"Fundamentos de Programación"}]""",
            AsignaturaDestino = "TI101",
            InstitucionOrigen = "UNIVERSIDAD TECNICA DE AMBATO",
            Fecha = new DateOnly(2025, 6, 15)
        };
        db.Precedentes.Add(precedente);
        await db.SaveChangesAsync();

        var service = new PrecedenteService(db);

        // Buscar con asignatura origen distinta
        var request = new BuscarPrecedenteRequest(
            new[] { "SW999" },
            "TI101",
            "Universidad Tecnica de Ambato");

        var resultados = await service.BuscarAsync(request);

        resultados.Should().BeEmpty();
    }

    [Fact]
    public async Task BuscarAsync_institucion_diferente_devuelve_vacio()
    {
        await using var db = CrearDb();

        var precedente = new Precedente
        {
            Resolucion = "RES-FISEI-2025-031",
            AsignaturasOrigenJson = """[{"Codigo":"SW101","Nombre":"Fundamentos de Programación"}]""",
            AsignaturaDestino = "TI101",
            InstitucionOrigen = "UNIVERSIDAD TECNICA DE AMBATO",
            Fecha = new DateOnly(2025, 6, 15)
        };
        db.Precedentes.Add(precedente);
        await db.SaveChangesAsync();

        var service = new PrecedenteService(db);

        var request = new BuscarPrecedenteRequest(
            new[] { "SW101" },
            "TI101",
            "OTRA UNIVERSIDAD");

        var resultados = await service.BuscarAsync(request);

        resultados.Should().BeEmpty();
    }

    [Fact]
    public async Task BuscarAsync_asignatura_destino_diferente_devuelve_vacio()
    {
        await using var db = CrearDb();

        var precedente = new Precedente
        {
            Resolucion = "RES-FISEI-2025-031",
            AsignaturasOrigenJson = """[{"Codigo":"SW101","Nombre":"Fundamentos de Programación"}]""",
            AsignaturaDestino = "TI101",
            InstitucionOrigen = "UNIVERSIDAD TECNICA DE AMBATO",
            Fecha = new DateOnly(2025, 6, 15)
        };
        db.Precedentes.Add(precedente);
        await db.SaveChangesAsync();

        var service = new PrecedenteService(db);

        var request = new BuscarPrecedenteRequest(
            new[] { "SW101" },
            "TI999",
            "Universidad Tecnica de Ambato");

        var resultados = await service.BuscarAsync(request);

        resultados.Should().BeEmpty();
    }

    [Fact]
    public async Task BuscarAsync_orden_asignaturas_no_importa()
    {
        await using var db = CrearDb();

        var precedente = new Precedente
        {
            Resolucion = "RES-FISEI-2025-031",
            AsignaturasOrigenJson = """[{"Codigo":"SW101","Nombre":"Fundamentos de Programación"},{"Codigo":"SW102","Nombre":"Programación II"}]""",
            AsignaturaDestino = "TI101",
            InstitucionOrigen = "UNIVERSIDAD TECNICA DE AMBATO",
            Fecha = new DateOnly(2025, 6, 15)
        };
        db.Precedentes.Add(precedente);
        await db.SaveChangesAsync();

        var service = new PrecedenteService(db);

        // Buscar en orden inverso
        var request = new BuscarPrecedenteRequest(
            new[] { "SW102", "SW101" },
            "TI101",
            "Universidad Tecnica de Ambato");

        var resultados = await service.BuscarAsync(request);

        resultados.Should().HaveCount(1);
    }

    [Fact]
    public async Task BuscarAsync_lista_vacia_origen_devuelve_vacio()
    {
        await using var db = CrearDb();
        var service = new PrecedenteService(db);

        var request = new BuscarPrecedenteRequest(
            Array.Empty<string>(),
            "TI101",
            "Universidad Tecnica de Ambato");

        var resultados = await service.BuscarAsync(request);

        resultados.Should().BeEmpty();
    }

    [Fact]
    public async Task BuscarAsync_multiple_precedentes_solo_coincide_exacto()
    {
        await using var db = CrearDb();

        db.Precedentes.AddRange(
            new Precedente
            {
                Resolucion = "RES-FISEI-2025-031",
                AsignaturasOrigenJson = """[{"Codigo":"SW101","Nombre":"Fundamentos de Programación"}]""",
                AsignaturaDestino = "TI101",
                InstitucionOrigen = "UNIVERSIDAD TECNICA DE AMBATO",
                Fecha = new DateOnly(2025, 6, 15)
            },
            new Precedente
            {
                Resolucion = "RES-FISEI-2024-015",
                AsignaturasOrigenJson = """[{"Codigo":"SW101","Nombre":"Fundamentos de Programación"}]""",
                AsignaturaDestino = "TI101",
                InstitucionOrigen = "OTRA UNIVERSIDAD",
                Fecha = new DateOnly(2024, 3, 10)
            },
            new Precedente
            {
                Resolucion = "RES-FISEI-2023-007",
                AsignaturasOrigenJson = """[{"Codigo":"SW102","Nombre":"Programación II"}]""",
                AsignaturaDestino = "TI101",
                InstitucionOrigen = "UNIVERSIDAD TECNICA DE AMBATO",
                Fecha = new DateOnly(2023, 11, 5)
            }
        );
        await db.SaveChangesAsync();

        var service = new PrecedenteService(db);

        var request = new BuscarPrecedenteRequest(
            new[] { "SW101" },
            "TI101",
            "Universidad Tecnica de Ambato");

        var resultados = await service.BuscarAsync(request);

        resultados.Should().HaveCount(1);
        resultados.First().Resolucion.Should().Be("RES-FISEI-2025-031");
    }

    [Fact]
    public async Task RegistrarAsync_crea_precedente_y_devuelve_id()
    {
        await using var db = CrearDb();
        var service = new PrecedenteService(db);

        var id = await service.RegistrarAsync(
            "RES-FISEI-2026-001",
            new[] { ("SW101", "Fundamentos de Programación"), ("SW102", "Programación II") },
            "TI101",
            "Universidad Tecnica de Ambato",
            new DateOnly(2026, 1, 20));

        id.Should().NotBe(Guid.Empty);

        var guardado = await db.Precedentes.FindAsync(id);
        guardado.Should().NotBeNull();
        guardado!.Resolucion.Should().Be("RES-FISEI-2026-001");
        guardado.AsignaturaDestino.Should().Be("TI101");
    }
}