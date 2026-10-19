using Siha.MsA.Api.Domain.Entities;

namespace Siha.MsA.Api.Security;

/// <summary>Emisor de tokens JWT firmados con claims de identidad y rol (US-01, CA2).</summary>
public interface IJwtTokenService
{
    string CrearToken(Usuario usuario, string rolNombre, string rolId);

    DateTimeOffset ObtenerExpiracion();
}
