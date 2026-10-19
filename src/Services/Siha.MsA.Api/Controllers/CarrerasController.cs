using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Siha.Contracts;
using Siha.MsA.Api.Contracts;
using Siha.MsA.Api.Contracts.Dtos;
using Siha.MsA.Api.Data;
using Siha.MsA.Api.Domain.Entities;

namespace Siha.MsA.Api.Controllers;

/// <summary>CRUD de carreras del catálogo académico (US-03, tarea S1-J04).</summary>
[ApiController]
[Route("api/carreras")]
[Authorize]
public class CarrerasController : ControllerBase
{
    private readonly SihaDbContextA _db;

    public CarrerasController(SihaDbContextA db)
    {
        _db = db;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CarreraDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CarreraDto>>> Listar()
    {
        var carreras = await _db.Carreras.AsNoTracking().OrderBy(c => c.Codigo).ToListAsync();
        return Ok(carreras.Select(Mapeos.ACarreraDto));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CarreraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CarreraDto>> Obtener(int id)
    {
        var carrera = await _db.Carreras.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        return carrera is null ? NotFound() : Ok(Mapeos.ACarreraDto(carrera));
    }

    [HttpPost]
    [Authorize(Roles = RolesSistema.Administrador)]
    [ProducesResponseType(typeof(CarreraDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CarreraDto>> Crear([FromBody] CrearCarreraRequest request)
    {
        var codigo = request.Codigo.Trim().ToUpperInvariant();
        if (await _db.Carreras.AnyAsync(c => c.Codigo == codigo))
        {
            return Conflict(new { mensaje = $"Ya existe una carrera con el código {codigo}." });
        }

        var carrera = new Carrera
        {
            Codigo = codigo,
            Nombre = request.Nombre.Trim(),
            Facultad = request.Facultad,
            Activa = request.Activa
        };

        _db.Carreras.Add(carrera);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Obtener), new { id = carrera.Id }, Mapeos.ACarreraDto(carrera));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = RolesSistema.Administrador)]
    [ProducesResponseType(typeof(CarreraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CarreraDto>> Actualizar(int id, [FromBody] ActualizarCarreraRequest request)
    {
        var carrera = await _db.Carreras.FirstOrDefaultAsync(c => c.Id == id);
        if (carrera is null)
        {
            return NotFound();
        }

        carrera.Nombre = request.Nombre.Trim();
        carrera.Facultad = request.Facultad;
        carrera.Activa = request.Activa;

        await _db.SaveChangesAsync();
        return Ok(Mapeos.ACarreraDto(carrera));
    }
}
