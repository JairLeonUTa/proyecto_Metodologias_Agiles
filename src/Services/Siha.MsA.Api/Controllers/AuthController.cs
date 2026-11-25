using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Siha.Contracts;
using Siha.MsA.Api.Contracts;
using Siha.MsA.Api.Contracts.Dtos;
using Siha.MsA.Api.Data;
using Siha.MsA.Api.Security;

namespace Siha.MsA.Api.Controllers;

/// <summary>
/// Autenticación institucional (US-01, tarea S1-J02 / S1-S02 en el borde del contrato).
/// Credenciales válidas de una cuenta activa generan un JWT con su rol; contraseña incorrecta
/// o cuenta inactiva producen 401 Unauthorized (CA2).
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly SihaDbContextA _db;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;

    public AuthController(SihaDbContextA db, IPasswordHasher hasher, IJwtTokenService jwt)
    {
        _db = db;
        _hasher = hasher;
        _jwt = jwt;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var correo = request.CorreoInstitucional.Trim().ToLowerInvariant();
        var usuario = await _db.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.CorreoInstitucional == correo);

        if (usuario is null || !usuario.Activo || !_hasher.Verify(request.Password, usuario.PasswordHash))
        {
            return Unauthorized(new { mensaje = "Credenciales inválidas o cuenta inactiva." });
        }

        usuario.UltimoAccesoUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        var rolNombre = usuario.Rol?.Nombre ?? RolesSistema.Operador;
        var token = _jwt.CrearToken(usuario, rolNombre, usuario.RolId.ToString());

        return Ok(new LoginResponse(token, _jwt.ObtenerExpiracion(), Mapeos.AUsuarioDto(usuario)));
    }

    /// <summary>
    /// Renueva la sesión: con un token todavía vigente entrega uno nuevo (otra hora más), sin volver a
    /// pedir la contraseña. Lo usa el aviso "Su sesión está por cerrarse" del frontend. Si la cuenta se
    /// desactivó o cambió de rol mientras tanto, se responde 401 o se emite el token con el rol actual.
    /// </summary>
    [HttpPost("renovar")]
    [Authorize(Roles = $"{RolesSistema.Administrador},{RolesSistema.Docente},{RolesSistema.Operador}")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Renovar()
    {
        var id = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(id, out var usuarioId))
        {
            return Unauthorized(new { mensaje = "La sesión no es válida. Vuelva a iniciar sesión." });
        }

        var usuario = await _db.Usuarios.Include(u => u.Rol).FirstOrDefaultAsync(u => u.Id == usuarioId);
        if (usuario is null || !usuario.Activo)
        {
            return Unauthorized(new { mensaje = "La cuenta ya no está activa. Vuelva a iniciar sesión." });
        }

        var rolNombre = usuario.Rol?.Nombre ?? RolesSistema.Operador;
        var token = _jwt.CrearToken(usuario, rolNombre, usuario.RolId.ToString());
        return Ok(new LoginResponse(token, _jwt.ObtenerExpiracion(), Mapeos.AUsuarioDto(usuario)));
    }

    /// <summary>Devuelve la identidad contenida en el token vigente (útil para el frontend).</summary>
    [HttpGet("yo")]
    [Authorize]
    public ActionResult<object> Yo() => Ok(new
    {
        id = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
        nombre = User.Identity?.Name,
        correo = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
        rol = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
    });
}
