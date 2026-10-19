using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Siha.MsA.Api.Domain.Entities;

namespace Siha.MsA.Api.Security;

/// <summary>
/// Emite tokens JWT firmados (HMAC-SHA256) inyectando los claims de identidad y el rol
/// (ADMINISTRADOR, DOCENTE u OPERADOR). Las cuentas inactivas o con credenciales inválidas
/// son rechazadas por el controlador antes de llegar aquí.
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _settings;

    public JwtTokenService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
    }

    public DateTimeOffset ObtenerExpiracion()
        => DateTimeOffset.UtcNow.AddMinutes(_settings.ExpiraMinutos);

    public string CrearToken(Usuario usuario, string rolNombre, string rolId)
    {
        var expiracion = ObtenerExpiracion();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, usuario.CorreoInstitucional),
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(ClaimTypes.Name, usuario.NombreCompleto),
            new(ClaimTypes.Email, usuario.CorreoInstitucional),
            new(ClaimTypes.Role, rolNombre),
            new("rolId", rolId)
        };

        var credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiracion.UtcDateTime,
            signingCredentials: credenciales);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
