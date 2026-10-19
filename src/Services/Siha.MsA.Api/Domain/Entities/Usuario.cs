namespace Siha.MsA.Api.Domain.Entities;

/// <summary>Cuenta de acceso al SIHA-IA. La contraseña se persiste únicamente como hash BCrypt.</summary>
public class Usuario
{
    public int Id { get; set; }

    /// <summary>Correo institucional único (clave natural de autenticación).</summary>
    public string CorreoInstitucional { get; set; } = string.Empty;

    public string NombreCompleto { get; set; } = string.Empty;

    /// <summary>Hash BCrypt; nunca se expone en respuestas de la API.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public int RolId { get; set; }
    public Rol? Rol { get; set; }

    public bool Activo { get; set; } = true;

    public DateTimeOffset FechaCreacionUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UltimoAccesoUtc { get; set; }
}
