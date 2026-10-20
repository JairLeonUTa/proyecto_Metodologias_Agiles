using Siha.MsA.Api.Contracts.Dtos;
using Siha.MsA.Api.Domain.Entities;
using Siha.MsA.Api.Services;

namespace Siha.MsA.Api.Contracts;

/// <summary>Conversión de entidades a DTO. Nunca expone el hash de contraseña (US-01, CA1).</summary>
public static class Mapeos
{
    public static UsuarioDto AUsuarioDto(Usuario u) => new(
        u.Id,
        u.CorreoInstitucional,
        u.NombreCompleto,
        u.Rol?.Nombre ?? string.Empty,
        u.RolId,
        u.Activo,
        u.FechaCreacionUtc,
        u.UltimoAccesoUtc);

    public static CarreraDto ACarreraDto(Carrera c) => new(c.Id, c.Codigo, c.Nombre, c.Facultad, c.Activa);

    public static MallaDto AMallaDto(MallaCurricular m, int totalAsignaturas) => new(
        m.Id,
        m.CarreraId,
        m.Carrera?.Codigo ?? string.Empty,
        m.Version,
        m.Anio,
        m.Fuente,
        m.RutaNormativa,
        m.Vigente,
        totalAsignaturas);

    public static AsignaturaDto AAsignaturaDto(Asignatura a)
    {
        var (error, advertencias) = ValidacionCatalogo.Evaluar(a);
        return new(
            a.Id,
            a.MallaId,
            a.Codigo,
            a.Nombre,
            a.Creditos,
            a.Horas,
            a.Semestre,
            error is null && advertencias.Count == 0,
            a.Unidades
                .OrderBy(u => u.Numero)
                .Select(u => new UnidadTematicaDto(u.Numero, u.Nombre, u.Horas, u.Contenidos))
                .ToList(),
            a.Activa)
        {
            Advertencias = error is null ? advertencias : new[] { error }
        };
    }

    public static RolDto ARolDto(Rol r) => new(r.Id, r.Nombre, r.Descripcion);
}
