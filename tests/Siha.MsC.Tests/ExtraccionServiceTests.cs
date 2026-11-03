using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Siha.MsC.Api.Contracts.Dtos;
using Siha.MsC.Api.Data;
using Siha.MsC.Api.Domain;
using Siha.MsC.Api.Domain.Entities;
using Siha.MsC.Api.Services;

namespace Siha.MsC.Tests;

public class ExtraccionServiceTests
{
    [Fact]
    public async Task Obtener_devuelve_la_extraccion_con_sus_unidades()
    {
        await using var db = CrearDb();
        var extraccion = NuevaExtraccion();
        extraccion.Unidades.Add(Unidad(2, "Unidad dos"));
        extraccion.Unidades.Add(Unidad(1, "Unidad uno"));
        db.Extracciones.Add(extraccion);
        await db.SaveChangesAsync();

        var servicio = new ExtraccionService(db);
        var obtenida = await servicio.ObtenerAsync(extraccion.Id);

        obtenida.Should().NotBeNull();
        obtenida!.Estado.Should().Be(EstadosExtraccion.PendienteConfirmacion);
        obtenida.Unidades.Select(u => u.Numero).Should().BeEquivalentTo(new[] { 2, 1 });
    }

    [Fact]
    public async Task Obtener_inexistente_devuelve_null()
    {
        await using var db = CrearDb();
        var servicio = new ExtraccionService(db);

        var obtenida = await servicio.ObtenerAsync(Guid.NewGuid());

        obtenida.Should().BeNull();
    }

    [Fact]
    public async Task Confirmar_corrige_creditos_horas_y_unidades()
    {
        await using var db = CrearDb();
        var extraccion = NuevaExtraccion();
        extraccion.Unidades.Add(Unidad(1, "Unidad original"));
        extraccion.Unidades.Add(Unidad(2, "Unidad que se elimina"));
        db.Extracciones.Add(extraccion);
        await db.SaveChangesAsync();

        var servicio = new ExtraccionService(db);
        var request = new ConfirmarExtraccionRequest(
            4,
            192,
            new[]
            {
                new UnidadExtraccionRequest(1, "Unidad corregida", 96, "Contenido corregido"),
                new UnidadExtraccionRequest(3, "Unidad nueva", 96, "Contenido nuevo")
            },
            "Corrección del operador");

        var resultado = await servicio.ConfirmarAsync(extraccion.Id, request, "operador@uta.edu.ec");

        resultado.Resultado.Should().Be(ResultadoConfirmacionExtraccion.Confirmada);

        var guardada = await db.Extracciones
            .Include(e => e.Unidades)
            .SingleAsync(e => e.Id == extraccion.Id);

        guardada.Creditos.Should().Be(4);
        guardada.Horas.Should().Be(192);
        guardada.Estado.Should().Be(EstadosExtraccion.Confirmada);
        guardada.ConfirmadoPor.Should().Be("operador@uta.edu.ec");
        guardada.ConfirmadoUtc.Should().NotBeNull();
        guardada.Observacion.Should().Be("Corrección del operador");
        guardada.Unidades.Select(u => u.Numero).Should().BeEquivalentTo(new[] { 1, 3 });
        guardada.Unidades.Single(u => u.Numero == 1).Nombre.Should().Be("Unidad corregida");
    }

    [Fact]
    public async Task Confirmar_inexistente_devuelve_no_encontrada()
    {
        await using var db = CrearDb();
        var servicio = new ExtraccionService(db);

        var resultado = await servicio.ConfirmarAsync(
            Guid.NewGuid(), new ConfirmarExtraccionRequest(3, 144, new[] { new UnidadExtraccionRequest(1, "U", 144) }), "operador");

        resultado.Resultado.Should().Be(ResultadoConfirmacionExtraccion.NoEncontrada);
    }

    [Fact]
    public async Task Confirmar_ya_confirmada_devuelve_conflicto()
    {
        await using var db = CrearDb();
        var extraccion = NuevaExtraccion(estado: EstadosExtraccion.Confirmada);
        db.Extracciones.Add(extraccion);
        await db.SaveChangesAsync();
        var servicio = new ExtraccionService(db);

        var resultado = await servicio.ConfirmarAsync(
            extraccion.Id, new ConfirmarExtraccionRequest(3, 144, new[] { new UnidadExtraccionRequest(1, "U", 144) }), "operador");

        resultado.Resultado.Should().Be(ResultadoConfirmacionExtraccion.YaConfirmada);
    }

    [Fact]
    public async Task Confirmar_pendiente_ocr_llm_devuelve_conflicto()
    {
        await using var db = CrearDb();
        var extraccion = NuevaExtraccion(estado: EstadosExtraccion.PendienteOcrLlm);
        db.Extracciones.Add(extraccion);
        await db.SaveChangesAsync();
        var servicio = new ExtraccionService(db);

        var resultado = await servicio.ConfirmarAsync(
            extraccion.Id, new ConfirmarExtraccionRequest(3, 144, new[] { new UnidadExtraccionRequest(1, "U", 144) }), "operador");

        resultado.Resultado.Should().Be(ResultadoConfirmacionExtraccion.PendienteOcrLlm);
    }

    [Fact]
    public async Task Confirmar_sin_unidades_rechaza_y_no_modifica_los_datos()
    {
        await using var db = CrearDb();
        var extraccion = NuevaExtraccion();
        db.Extracciones.Add(extraccion);
        await db.SaveChangesAsync();
        var servicio = new ExtraccionService(db);

        var resultado = await servicio.ConfirmarAsync(
            extraccion.Id, new ConfirmarExtraccionRequest(3, 144, Array.Empty<UnidadExtraccionRequest>()), "operador");

        resultado.Resultado.Should().Be(ResultadoConfirmacionExtraccion.DatosInvalidos);
        resultado.Errores.Should().Contain(e => e.Contains("al menos una unidad"));

        var guardada = await db.Extracciones.SingleAsync(e => e.Id == extraccion.Id);
        guardada.Estado.Should().Be(EstadosExtraccion.PendienteConfirmacion);
        guardada.Creditos.Should().BeNull();
    }

    [Fact]
    public async Task Confirmar_creditos_fuera_de_rango_rechaza()
    {
        await using var db = CrearDb();
        var extraccion = NuevaExtraccion();
        db.Extracciones.Add(extraccion);
        await db.SaveChangesAsync();
        var servicio = new ExtraccionService(db);

        var resultado = await servicio.ConfirmarAsync(
            extraccion.Id, new ConfirmarExtraccionRequest(0, 144, new[] { new UnidadExtraccionRequest(1, "U", 144) }), "operador");

        resultado.Resultado.Should().Be(ResultadoConfirmacionExtraccion.DatosInvalidos);
        resultado.Errores.Should().Contain(e => e.Contains("créditos"));
    }

    [Fact]
    public async Task Confirmar_numeros_de_unidad_duplicados_rechaza()
    {
        await using var db = CrearDb();
        var extraccion = NuevaExtraccion();
        db.Extracciones.Add(extraccion);
        await db.SaveChangesAsync();
        var servicio = new ExtraccionService(db);

        var resultado = await servicio.ConfirmarAsync(
            extraccion.Id,
            new ConfirmarExtraccionRequest(3, 144, new[]
            {
                new UnidadExtraccionRequest(1, "Uno", 72),
                new UnidadExtraccionRequest(1, "Otra vez uno", 72)
            }),
            "operador");

        resultado.Resultado.Should().Be(ResultadoConfirmacionExtraccion.DatosInvalidos);
        resultado.Errores.Should().Contain(e => e.Contains("duplicados"));
    }

    [Fact]
    public async Task Listar_bandeja_ordena_por_fecha_descendente_y_filtra_por_estado()
    {
        await using var db = CrearDb();
        var antigua = NuevaExtraccion(confianza: 0.94m, estado: EstadosExtraccion.Confirmada);
        antigua.CreadoUtc = DateTimeOffset.UtcNow.AddHours(-2);
        var reciente = NuevaExtraccion(confianza: 0.68m);
        reciente.CreadoUtc = DateTimeOffset.UtcNow;
        var escaneada = NuevaExtraccion(confianza: 0m, estado: EstadosExtraccion.PendienteOcrLlm);
        escaneada.CreadoUtc = DateTimeOffset.UtcNow.AddHours(-1);
        db.Extracciones.AddRange(antigua, reciente, escaneada);
        await db.SaveChangesAsync();
        var servicio = new ExtraccionService(db);

        var todas = await servicio.ListarAsync();
        var pendientes = await servicio.ListarAsync("pendiente_confirmacion");
        var delExpediente = await servicio.ListarAsync(expedienteId: escaneada.ExpedienteId);

        todas.Select(e => e.Id).Should().ContainInOrder(reciente.Id, escaneada.Id, antigua.Id);
        pendientes.Should().ContainSingle().Which.Id.Should().Be(reciente.Id);
        delExpediente.Should().ContainSingle().Which.Estado.Should().Be(EstadosExtraccion.PendienteOcrLlm);
    }

    private static SihaDbContextC CrearDb()
        => new(new DbContextOptionsBuilder<SihaDbContextC>()
            .UseInMemoryDatabase($"siha-msc-extracciones-{Guid.NewGuid():N}")
            .Options);

    private static Extraccion NuevaExtraccion(
        decimal confianza = 0.65m,
        string estado = EstadosExtraccion.PendienteConfirmacion)
        => new()
        {
            ExpedienteId = Guid.NewGuid(),
            DocumentoId = Guid.NewGuid(),
            CodigoAsignaturaOrigen = "SW101",
            NombreAsignaturaOrigen = "Fundamentos de Programación",
            Confianza = confianza,
            Estado = estado
        };

    private static ExtraccionUnidad Unidad(int numero, string nombre)
        => new()
        {
            Numero = numero,
            Nombre = nombre,
            Horas = 96,
            Contenidos = $"Contenidos {numero}"
        };
}
