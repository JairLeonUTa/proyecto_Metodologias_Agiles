using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Siha.Contracts;
using Siha.MsA.Api.Contracts;
using Siha.MsA.Api.Contracts.Dtos;
using Siha.MsA.Api.Data;
using Siha.MsA.Api.Domain.Entities;
using Siha.MsA.Api.Security;

namespace Siha.MsA.Api.Controllers;

/// <summary>
/// Gestión de cuentas y roles (US-01, tarea S1-J02).
/// Solo un Administrador gestiona cuentas; Docente y Operador reciben 403 (CA3).
/// El hash BCrypt nunca se expone en las respuestas (CA1).
/// </summary>
[ApiController]
[Route("api/usuarios")]
[Authorize(Roles = RolesSistema.Administrador)]
public class UsuariosController : ControllerBase
{
    private readonly SihaDbContextA _db;
    private readonly IPasswordHasher _hasher;

    public UsuariosController(SihaDbContextA db, IPasswordHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<UsuarioDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<UsuarioDto>>> Listar([FromQuery] bool? activo)
    {
        var query = _db.Usuarios.Include(u => u.Rol).AsNoTracking().AsQueryable();
        if (activo.HasValue)
        {
            query = query.Where(u => u.Activo == activo.Value);
        }

        var usuarios = await query.OrderBy(u => u.NombreCompleto).ToListAsync();
        return Ok(usuarios.Select(Mapeos.AUsuarioDto));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioDto>> Obtener(int id)
    {
        var usuario = await _db.Usuarios.Include(u => u.Rol).AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id);

        return usuario is null ? NotFound() : Ok(Mapeos.AUsuarioDto(usuario));
    }

    [HttpPost]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UsuarioDto>> Crear([FromBody] CrearUsuarioRequest request)
    {
        var rol = await _db.Roles.FirstOrDefaultAsync(r => r.Nombre == request.Rol);
        if (rol is null)
        {
            return BadRequest(new { mensaje = $"Rol inválido. Valores permitidos: {string.Join(", ", RolesSistema.Todos)}." });
        }

        var correo = request.CorreoInstitucional.Trim().ToLowerInvariant();
        var existe = await _db.Usuarios.AnyAsync(u => u.CorreoInstitucional == correo);
        if (existe)
        {
            return Conflict(new { mensaje = "Ya existe una cuenta con ese correo institucional." });
        }

        var usuario = new Usuario
        {
            CorreoInstitucional = correo,
            NombreCompleto = request.NombreCompleto.Trim(),
            PasswordHash = _hasher.Hash(request.Password),
            RolId = rol.Id,
            Activo = request.Activo
        };

        _db.Usuarios.Add(usuario);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
        {
            return Conflict(new { mensaje = "Ya existe una cuenta con ese correo institucional." });
        }

        usuario.Rol = rol;
        return CreatedAtAction(nameof(Obtener), new { id = usuario.Id }, Mapeos.AUsuarioDto(usuario));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioDto>> Actualizar(int id, [FromBody] ActualizarUsuarioRequest request)
    {
        var usuario = await _db.Usuarios.Include(u => u.Rol).FirstOrDefaultAsync(u => u.Id == id);
        if (usuario is null)
        {
            return NotFound();
        }

        var rol = await _db.Roles.FirstOrDefaultAsync(r => r.Nombre == request.Rol);
        if (rol is null)
        {
            return BadRequest(new { mensaje = $"Rol inválido. Valores permitidos: {string.Join(", ", RolesSistema.Todos)}." });
        }

        usuario.NombreCompleto = request.NombreCompleto.Trim();
        usuario.RolId = rol.Id;
        usuario.Rol = rol;
        usuario.Activo = request.Activo;

        await _db.SaveChangesAsync();
        return Ok(Mapeos.AUsuarioDto(usuario));
    }

    [HttpPatch("{id:int}/estado")]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioDto>> CambiarEstado(int id, [FromBody] CambiarEstadoUsuarioRequest request)
    {
        var usuario = await _db.Usuarios.Include(u => u.Rol).FirstOrDefaultAsync(u => u.Id == id);
        if (usuario is null)
        {
            return NotFound();
        }

        usuario.Activo = request.Activo;
        await _db.SaveChangesAsync();
        return Ok(Mapeos.AUsuarioDto(usuario));
    }

    /// <summary>Baja lógica de la cuenta (se conserva la trazabilidad de los expedientes asociados).</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Desactivar(int id)
    {
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == id);
        if (usuario is null)
        {
            return NotFound();
        }

        usuario.Activo = false;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
