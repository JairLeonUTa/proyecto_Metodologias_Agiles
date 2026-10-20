using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Siha.Contracts;
using Siha.MsA.Api.Contracts;
using Siha.MsA.Api.Contracts.Dtos;
using Siha.MsA.Api.Data;
using Siha.MsA.Api.Domain.Entities;
using Siha.MsA.Api.Services;

namespace Siha.MsA.Api.Controllers;

/// <summary>
/// CRUD de asignaturas y unidades temáticas, e importación masiva del catálogo estructurado
/// (US-03, tarea S1-J05).
/// </summary>
[ApiController]
[Route("api/asignaturas")]
[Authorize]
public class AsignaturasController : ControllerBase
{
    private readonly SihaDbContextA _db;
    private readonly ICatalogoService _catalogo;

    public AsignaturasController(SihaDbContextA db, ICatalogoService catalogo)
    {
        _db = db;
        _catalogo = catalogo;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AsignaturaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AsignaturaDto>>> Listar(
        [FromQuery] int? mallaId,
        [FromQuery] string? codigo)
    {
        var query = _db.Asignaturas.Include(a => a.Unidades).AsNoTracking().AsQueryable();

        if (mallaId.HasValue)
        {
            query = query.Where(a => a.MallaId == mallaId.Value);
        }

        if (!string.IsNullOrWhiteSpace(codigo))
        {
            var codigoNormalizado = codigo.Trim().ToUpperInvariant();
            query = query.Where(a => a.Codigo.Trim().ToUpper() == codigoNormalizado);
        }

        var asignaturas = await query.OrderBy(a => a.Semestre).ThenBy(a => a.Codigo).ToListAsync();
        return Ok(asignaturas.Select(Mapeos.AAsignaturaDto));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AsignaturaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AsignaturaDto>> Obtener(int id)
    {
        var asignatura = await _db.Asignaturas.Include(a => a.Unidades).AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);

        return asignatura is null ? NotFound() : Ok(Mapeos.AAsignaturaDto(asignatura));
    }

    [HttpPost]
    [Authorize(Roles = RolesSistema.Administrador)]
    [ProducesResponseType(typeof(AsignaturaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AsignaturaDto>> Crear([FromBody] CrearAsignaturaRequest request)
    {
        var malla = await _db.Mallas.FirstOrDefaultAsync(m => m.Id == request.MallaId);
        if (malla is null)
        {
            return BadRequest(new { mensaje = $"La malla {request.MallaId} no existe." });
        }

        var (error, advertencias) = ValidacionCatalogo.Validar(request.Codigo, request.Nombre,
            request.Creditos, request.Horas, request.Semestre, request.Unidades ?? Array.Empty<UnidadTematicaRequest>());
        if (error is not null)
            return BadRequest(new { mensaje = error });

        var codigo = request.Codigo.Trim().ToUpperInvariant();
        if (await _db.Asignaturas.AnyAsync(a => a.MallaId == request.MallaId && a.Codigo.Trim().ToUpper() == codigo))
        {
            return Conflict(new { mensaje = $"El código {codigo} ya existe en la malla {malla.Version}." });
        }

        var asignatura = new Asignatura
        {
            MallaId = request.MallaId,
            Codigo = codigo,
            Nombre = request.Nombre.Trim(),
            Creditos = request.Creditos,
            Horas = request.Horas,
            Semestre = request.Semestre,
            PlanCompleto = advertencias.Count == 0
        };

        AplicarUnidades(asignatura, request.Unidades);

        _db.Asignaturas.Add(asignatura);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Obtener), new { id = asignatura.Id }, Mapeos.AAsignaturaDto(asignatura));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = RolesSistema.Administrador)]
    [ProducesResponseType(typeof(AsignaturaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AsignaturaDto>> Actualizar(int id, [FromBody] ActualizarAsignaturaRequest request)
    {
        var asignatura = await _db.Asignaturas.Include(a => a.Unidades).FirstOrDefaultAsync(a => a.Id == id);
        if (asignatura is null)
        {
            return NotFound();
        }

        var unidades = request.Unidades ?? asignatura.Unidades
            .Select(u => new UnidadTematicaRequest(u.Numero, u.Nombre, u.Horas, u.Contenidos)).ToArray();
        var (error, advertencias) = ValidacionCatalogo.Validar(asignatura.Codigo, request.Nombre,
            request.Creditos, request.Horas, request.Semestre, unidades);
        if (error is not null)
            return BadRequest(new { mensaje = error });

        asignatura.Nombre = request.Nombre.Trim();
        asignatura.Creditos = request.Creditos;
        asignatura.Horas = request.Horas;
        asignatura.Semestre = request.Semestre;

        if (request.Unidades is not null)
        {
            _db.UnidadesTematicas.RemoveRange(asignatura.Unidades);
            asignatura.Unidades.Clear();
            AplicarUnidades(asignatura, request.Unidades);
        }

        asignatura.PlanCompleto = advertencias.Count == 0;
        await _db.SaveChangesAsync();
        return Ok(Mapeos.AAsignaturaDto(asignatura));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = RolesSistema.Administrador)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(int id)
    {
        var asignatura = await _db.Asignaturas.FindAsync(id);
        if (asignatura is null) return NotFound();
        asignatura.Activa = false;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Importación masiva del catálogo estructurado (43 planes analíticos de Software).
    /// Rechaza códigos duplicados, valida 48 h/crédito y marca los planes incompletos.
    /// </summary>
    [HttpPost("importar")]
    [Authorize(Roles = RolesSistema.Administrador)]
    [ProducesResponseType(typeof(ImportacionResultadoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportacionResultadoDto>> Importar([FromBody] ImportarCatalogoRequest request)
    {
        var (ok, error, resultado) = await _catalogo.ImportarAsync(request);
        if (!ok)
        {
            return BadRequest(new { mensaje = error });
        }

        return Ok(resultado);
    }

    private static void AplicarUnidades(Asignatura asignatura, IReadOnlyCollection<UnidadTematicaRequest>? unidades)
    {
        if (unidades is null)
        {
            return;
        }

        foreach (var unidad in unidades.OrderBy(u => u.Numero))
        {
            asignatura.Unidades.Add(new UnidadTematica
            {
                Numero = unidad.Numero,
                Nombre = unidad.Nombre.Trim(),
                Horas = unidad.Horas,
                Contenidos = unidad.Contenidos ?? string.Empty
            });
        }
    }
}
