using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Siha.Contracts;
using Siha.MsA.Api.Data;
using Siha.MsA.Api.Grpc;

namespace Siha.MsA.Api.GrpcServices;

/// <summary>
/// Servicio gRPC interno del catálogo (puerto h2c de MS A):
///  - El agente de MS C pide la malla vigente de la carrera de destino con unidades y contenidos.
///  - MS B verifica que los docentes revisores del certificado SIHA son usuarios activos con rol DOCENTE.
/// </summary>
public class CatalogoGrpcService : CatalogoGrpc.CatalogoGrpcBase
{
    private readonly SihaDbContextA _db;
    private readonly ILogger<CatalogoGrpcService> _logger;

    public CatalogoGrpcService(SihaDbContextA db, ILogger<CatalogoGrpcService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public override async Task<MallaVigente> ObtenerMallaVigente(SolicitudMalla request, ServerCallContext context)
    {
        var carrera = await BuscarCarreraAsync(request.CarreraCodigo, context.CancellationToken);
        var codigo = carrera?.Codigo ?? (request.CarreraCodigo ?? string.Empty).Trim().ToUpperInvariant();
        var respuesta = new MallaVigente { CarreraCodigo = codigo };
        if (carrera is null)
        {
            return respuesta;
        }

        var malla = await _db.Mallas.AsNoTracking()
            .Where(m => m.CarreraId == carrera.Id)
            .OrderByDescending(m => m.Vigente).ThenByDescending(m => m.Anio)
            .FirstOrDefaultAsync(context.CancellationToken);
        if (malla is null)
        {
            return respuesta;
        }

        var asignaturas = await _db.Asignaturas.AsNoTracking()
            .Include(a => a.Unidades)
            .Where(a => a.MallaId == malla.Id && a.Activa)
            .OrderBy(a => a.Semestre).ThenBy(a => a.Codigo)
            .ToListAsync(context.CancellationToken);

        respuesta.Encontrada = true;
        respuesta.CarreraNombre = carrera.Nombre;
        respuesta.Version = malla.Version;
        foreach (var a in asignaturas)
        {
            var item = new AsignaturaMalla { Codigo = a.Codigo, Nombre = a.Nombre, Creditos = a.Creditos, Semestre = a.Semestre };
            item.Unidades.AddRange(a.Unidades.OrderBy(u => u.Numero).Select(u => new UnidadMalla
            {
                Numero = u.Numero, Nombre = u.Nombre, Horas = u.Horas, Contenidos = u.Contenidos ?? string.Empty
            }));
            respuesta.Asignaturas.Add(item);
        }

        _logger.LogInformation("gRPC ObtenerMallaVigente {Carrera} v{Version}: {Asignaturas} asignatura(s).",
            codigo, malla.Version, asignaturas.Count);
        return respuesta;
    }

    public override async Task<CarreraResuelta> ResolverCarrera(SolicitudCarrera request, ServerCallContext context)
    {
        var carrera = await BuscarCarreraAsync(request.Nombre, context.CancellationToken);
        return carrera is null
            ? new CarreraResuelta()
            : new CarreraResuelta { Encontrada = true, Codigo = carrera.Codigo, Nombre = carrera.Nombre };
    }

    /// <summary>Código exacto o nombre equivalente: "Software" = "Ingeniería de Software".</summary>
    private async Task<Siha.MsA.Api.Domain.Entities.Carrera?> BuscarCarreraAsync(string? texto, CancellationToken cancellationToken)
    {
        var buscado = (texto ?? string.Empty).Trim();
        if (buscado.Length == 0) return null;
        var carreras = await _db.Carreras.AsNoTracking().ToListAsync(cancellationToken);
        var clave = ClaveCarrera(buscado);
        return carreras.FirstOrDefault(c => string.Equals(c.Codigo, buscado, StringComparison.OrdinalIgnoreCase))
            ?? carreras.FirstOrDefault(c => ClaveCarrera(c.Nombre) == clave)
            ?? carreras.FirstOrDefault(c => clave.Length >= 5 && (ClaveCarrera(c.Nombre).Contains(clave) || clave.Contains(ClaveCarrera(c.Nombre))));
    }

    public static string ClaveCarrera(string nombre)
    {
        var sinTildes = new string(nombre.Normalize(System.Text.NormalizationForm.FormD)
            .Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray()).ToLowerInvariant();
        var palabras = System.Text.RegularExpressions.Regex.Split(sinTildes, "[^a-z0-9]+")
            .Where(p => p.Length > 0 && p is not ("ingenieria" or "en" or "de" or "la" or "y" or "carrera" or "las" or "los"));
        return string.Join(" ", palabras.OrderBy(p => p));
    }

    public override async Task<RespuestaDocentes> VerificarDocentes(SolicitudDocentes request, ServerCallContext context)
    {
        var correos = request.Correos.Select(c => (c ?? string.Empty).Trim().ToLowerInvariant()).Distinct().ToList();
        var usuarios = await _db.Usuarios.AsNoTracking()
            .Include(u => u.Rol)
            .Where(u => correos.Contains(u.CorreoInstitucional.ToLower()))
            .ToListAsync(context.CancellationToken);

        var respuesta = new RespuestaDocentes();
        foreach (var correo in correos)
        {
            var u = usuarios.FirstOrDefault(x => x.CorreoInstitucional.ToLower() == correo);
            respuesta.Docentes.Add(new DocenteVerificado
            {
                Correo = correo,
                Existe = u is not null,
                EsDocente = u?.Rol?.Nombre == RolesSistema.Docente,
                Activo = u?.Activo ?? false,
                NombreCompleto = u?.NombreCompleto ?? string.Empty
            });
        }

        return respuesta;
    }
}
