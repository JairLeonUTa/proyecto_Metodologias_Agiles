namespace Siha.MsA.Api.Domain.Entities;

/// <summary>Rol del sistema para el control de acceso basado en roles (RBAC).</summary>
public class Rol
{
    public int Id { get; set; }

    /// <summary>ADMINISTRADOR | DOCENTE | OPERADOR.</summary>
    public string Nombre { get; set; } = string.Empty;

    public string Descripcion { get; set; } = string.Empty;

    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
}
