using System.ComponentModel.DataAnnotations;

namespace Siha.MsA.Api.Contracts.Dtos;

public record LoginRequest(
    [param: Required, EmailAddress] string CorreoInstitucional,
    [param: Required, MinLength(8)] string Password);

public record UsuarioDto(
    int Id,
    string CorreoInstitucional,
    string NombreCompleto,
    string Rol,
    int RolId,
    bool Activo,
    DateTimeOffset FechaCreacionUtc,
    DateTimeOffset? UltimoAccesoUtc);

public record LoginResponse(
    string Token,
    DateTimeOffset ExpiraUtc,
    UsuarioDto Usuario);

public record RolDto(int Id, string Nombre, string Descripcion);

public record CrearUsuarioRequest(
    [param: Required, EmailAddress] string CorreoInstitucional,
    [param: Required, MinLength(3)] string NombreCompleto,
    [param: Required, MinLength(8)] string Password,
    [param: Required] string Rol,
    bool Activo = true);

public record ActualizarUsuarioRequest(
    [param: Required, MinLength(3)] string NombreCompleto,
    [param: Required] string Rol,
    bool Activo = true);

public record CambiarEstadoUsuarioRequest(bool Activo);
