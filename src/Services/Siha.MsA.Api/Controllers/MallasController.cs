using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Siha.Contracts;
using Siha.MsA.Api.Contracts;
using Siha.MsA.Api.Contracts.Dtos;
using Siha.MsA.Api.Data;
using Siha.MsA.Api.Domain.Entities;

namespace Siha.MsA.Api.Controllers;

/// <summary>CRUD de versiones de mallas curriculares (US-03, tarea S1-J04).</summary>
[ApiController]
[Route("api/mallas")]
[Authorize]
public class MallasController : ControllerBase
{
    private readonly SihaDbContextA _db;

    public MallasController(SihaDbContextA db)
    {
        _db = db;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<MallaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<MallaDto>>> Listar([FromQuery] int? carreraId)
    {
        var query = _db.Mallas.Include(m => m.Carrera).Include(m => m.Asignaturas).AsNoTracking().AsQueryable();
        if (carreraId.HasValue)
        {
            query = query.Where(m => m.CarreraId == carreraId.Value);
        }

        var mallas = await query.OrderBy(m => m.CarreraId).ThenByDescending(m => m.Anio).ToListAsync();
        return Ok(mallas.Select(m => Mapeos.AMallaDto(m, m.Asignaturas.Count)));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(MallaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MallaDto>> Obtener(int id)
    {
        var malla = await _db.Mallas.Include(m => m.Carrera).Include(m => m.Asignaturas).AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);

        return malla is null ? NotFound() : Ok(Mapeos.AMallaDto(malla, malla.Asignaturas.Count));
    }

    [HttpPost]
    [Authorize(Roles = RolesSistema.Administrador)]
    [ProducesResponseType(typeof(MallaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MallaDto>> Crear([FromBody] CrearMallaRequest request)
    {
        var carrera = await _db.Carreras.FirstOrDefaultAsync(c => c.Id == request.CarreraId);
        if (carrera is null)
        {
            return BadRequest(new { mensaje = $"La carrera {request.CarreraId} no existe." });
        }

        var error = Validar(request.Version, request.Anio, request.Fuente, request.RutaNormativa);
        if (error is not null) return BadRequest(new { mensaje = error });

        var version = request.Version.Trim().ToUpperInvariant();
        if (await _db.Mallas.AnyAsync(m => m.CarreraId == request.CarreraId && m.Version.Trim().ToUpper() == version))
        {
            return Conflict(new { mensaje = $"La carrera {carrera.Codigo} ya tiene la versión de malla {version}." });
        }

        var malla = new MallaCurricular
        {
            CarreraId = request.CarreraId,
            Version = version,
            Anio = request.Anio,
            Fuente = request.Fuente.Trim(),
            RutaNormativa = request.RutaNormativa?.Trim().ToUpperInvariant(),
            Vigente = request.Vigente,
            Carrera = carrera
        };

        _db.Mallas.Add(malla);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Obtener), new { id = malla.Id }, Mapeos.AMallaDto(malla, 0));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = RolesSistema.Administrador)]
    [ProducesResponseType(typeof(MallaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MallaDto>> Actualizar(int id, [FromBody] ActualizarMallaRequest request)
    {
        var malla = await _db.Mallas.Include(m => m.Carrera).Include(m => m.Asignaturas)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (malla is null) return NotFound();
        var error = Validar(request.Version, request.Anio, request.Fuente, request.RutaNormativa);
        if (error is not null) return BadRequest(new { mensaje = error });

        var version = request.Version.Trim().ToUpperInvariant();
        if (await _db.Mallas.AnyAsync(m => m.Id != id && m.CarreraId == malla.CarreraId &&
            m.Version.Trim().ToUpper() == version))
            return Conflict(new { mensaje = $"La carrera ya tiene la versión de malla {version}." });

        malla.Version = version;
        malla.Anio = request.Anio;
        malla.Fuente = request.Fuente.Trim();
        malla.RutaNormativa = request.RutaNormativa?.Trim().ToUpperInvariant();
        malla.Vigente = request.Vigente;
        await _db.SaveChangesAsync();
        return Ok(Mapeos.AMallaDto(malla, malla.Asignaturas.Count));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = RolesSistema.Administrador)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(int id)
    {
        var malla = await _db.Mallas.FindAsync(id);
        if (malla is null) return NotFound();
        malla.Vigente = false;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static string? Validar(string version, int anio, string fuente, string? ruta) =>
        string.IsNullOrWhiteSpace(version) || version.Trim().Length > 20 || anio is < 1990 or > 2100 ||
        string.IsNullOrWhiteSpace(fuente) || fuente.Trim().Length > 200 || ruta?.Length > 20
            ? "Versión (máximo 20), año (1990–2100), fuente (máximo 200) y ruta (máximo 20) deben ser válidos."
            : null;
}
