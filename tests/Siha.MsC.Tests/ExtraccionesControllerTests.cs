using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Siha.MsC.Api.Contracts.Dtos;
using Siha.MsC.Api.Controllers;
using Siha.MsC.Api.Data;
using Siha.MsC.Api.Domain;
using Siha.MsC.Api.Domain.Entities;
using Siha.MsC.Api.Services;

namespace Siha.MsC.Tests;

public class ExtraccionesControllerTests
{
    [Fact]
    public async Task Obtener_inexistente_devuelve_404()
    {
        await using var db = CrearDb();
        var controlador = CrearControlador(db);

        var resultado = await controlador.Obtener(Guid.NewGuid(), CancellationToken.None);

        resultado.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Obtener_existente_devuelve_200_con_requiere_confirmacion()
    {
        await using var db = CrearDb();
        var extraccion = await SembrarAsync(db);
        var controlador = CrearControlador(db);

        var resultado = await controlador.Obtener(extraccion.Id, CancellationToken.None);

        var ok = resultado.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<ExtraccionResponse>().Subject;
        dto.Id.Should().Be(extraccion.Id);
        dto.RequiereConfirmacion.Should().BeTrue();
        dto.Estado.Should().Be(EstadosExtraccion.PendienteConfirmacion);
    }

    [Fact]
    public async Task Confirmar_sin_unidades_devuelve_400()
    {
        await using var db = CrearDb();
        var extraccion = await SembrarAsync(db);
        var controlador = CrearControlador(db);

        var resultado = await controlador.Confirmar(
            extraccion.Id,
            new ConfirmarExtraccionRequest(3, 144, Array.Empty<UnidadExtraccionRequest>()),
            CancellationToken.None);

        resultado.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Confirmar_valida_devuelve_200_y_registra_al_usuario()
    {
        await using var db = CrearDb();
        var extraccion = await SembrarAsync(db);
        var controlador = CrearControlador(db, "operador@uta.edu.ec");

        var resultado = await controlador.Confirmar(
            extraccion.Id,
            new ConfirmarExtraccionRequest(
                3, 144, new[] { new UnidadExtraccionRequest(1, "Unidad corregida", 144, "Tema") }),
            CancellationToken.None);

        var ok = resultado.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<ExtraccionResponse>().Subject;
        dto.Estado.Should().Be(EstadosExtraccion.Confirmada);
        dto.ConfirmadoPor.Should().Be("operador@uta.edu.ec");
        dto.RequiereConfirmacion.Should().BeFalse();
    }

    [Fact]
    public async Task Confirmar_ya_confirmada_devuelve_409()
    {
        await using var db = CrearDb();
        var extraccion = await SembrarAsync(db);
        var controlador = CrearControlador(db);

        var primera = await controlador.Confirmar(
            extraccion.Id,
            new ConfirmarExtraccionRequest(3, 144, new[] { new UnidadExtraccionRequest(1, "U", 144) }),
            CancellationToken.None);
        primera.Result.Should().BeOfType<OkObjectResult>();

        var segunda = await controlador.Confirmar(
            extraccion.Id,
            new ConfirmarExtraccionRequest(3, 144, new[] { new UnidadExtraccionRequest(1, "U", 144) }),
            CancellationToken.None);

        segunda.Result.Should().BeOfType<ConflictObjectResult>();
    }

    private static SihaDbContextC CrearDb()
        => new(new DbContextOptionsBuilder<SihaDbContextC>()
            .UseInMemoryDatabase($"siha-msc-controller-{Guid.NewGuid():N}")
            .Options);

    private static ExtraccionesController CrearControlador(SihaDbContextC db, string? usuario = null)
    {
        var identidad = new ClaimsIdentity(
            usuario is null ? Array.Empty<Claim>() : new[] { new Claim(ClaimTypes.Name, usuario) },
            usuario is null ? "anonimo" : "prueba");

        return new ExtraccionesController(new ExtraccionService(db))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identidad) }
            }
        };
    }

    private static async Task<Extraccion> SembrarAsync(SihaDbContextC db)
    {
        var extraccion = new Extraccion
        {
            ExpedienteId = Guid.NewGuid(),
            DocumentoId = Guid.NewGuid(),
            CodigoAsignaturaOrigen = "SW101",
            NombreAsignaturaOrigen = "Fundamentos de Programación",
            Confianza = 0.65m,
            Estado = EstadosExtraccion.PendienteConfirmacion
        };
        extraccion.Unidades.Add(new ExtraccionUnidad
        {
            Numero = 1,
            Nombre = "Unidad original",
            Horas = 144,
            Contenidos = "Contenido"
        });

        db.Extracciones.Add(extraccion);
        await db.SaveChangesAsync();
        return extraccion;
    }
}
