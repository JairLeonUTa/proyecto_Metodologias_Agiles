using Microsoft.EntityFrameworkCore;
using Siha.MsA.Api.Contracts.Dtos;
using Siha.MsA.Api.Data;
using Siha.MsA.Api.Domain.Entities;

namespace Siha.MsA.Api.Services;

public interface IReglasService
{
    Task<IReadOnlyCollection<ReglaDto>> ListarReglasAsync(string? rutaNormativa, string? tipoTramite);

    Task<(bool Ok, string? Error, ReglaDto? Regla)> CrearReglaAsync(CrearReglaRequest request);

    Task<ParametroEvaluacionDto?> ObtenerParametrosAsync(string rutaNormativa);

    Task<(bool Ok, string? Error, ParametroEvaluacionDto? Parametros)> ActualizarParametrosAsync(
        string rutaNormativa, ActualizarParametrosRequest request, string usuario);
}

/// <summary>
/// Reglas y umbrales de evaluación (US-02, tarea S1-J03).
/// Aplica la validación matemática δ ≤ θ y rangos [0, 100], sin modificar la configuración
/// previa cuando la petición es inválida.
/// </summary>
public class ReglasService : IReglasService
{
    private readonly SihaDbContextA _db;

    public ReglasService(SihaDbContextA db)
    {
        _db = db;
    }

    public async Task<IReadOnlyCollection<ReglaDto>> ListarReglasAsync(string? rutaNormativa, string? tipoTramite)
    {
        var query = _db.Reglas.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(rutaNormativa))
        {
            query = query.Where(r => r.RutaNormativa == rutaNormativa);
        }

        if (!string.IsNullOrWhiteSpace(tipoTramite))
        {
            query = query.Where(r => r.TipoTramite == tipoTramite);
        }

        return await query
            .OrderBy(r => r.RutaNormativa).ThenBy(r => r.Codigo)
            .Select(r => ToDto(r))
            .ToListAsync();
    }

    public async Task<(bool Ok, string? Error, ReglaDto? Regla)> CrearReglaAsync(CrearReglaRequest request)
    {
        if (!RutasNormativasValidas(request.RutaNormativa))
        {
            return (false, "La ruta normativa debe ser CAU-P-011 o CAU-P-122.", null);
        }

        var duplicada = await _db.Reglas.AnyAsync(r =>
            r.Codigo == request.Codigo &&
            r.RutaNormativa == request.RutaNormativa &&
            r.Version == request.Version);

        if (duplicada)
        {
            return (false, "Ya existe una regla con el mismo código, ruta normativa y versión.", null);
        }

        var regla = new Regla
        {
            Codigo = request.Codigo,
            RutaNormativa = request.RutaNormativa,
            TipoTramite = request.TipoTramite,
            Descripcion = request.Descripcion,
            Expresion = request.Expresion,
            Version = request.Version,
            Fuente = request.Fuente,
            EsNormativa = request.EsNormativa,
            VigenteDesde = request.VigenteDesde,
            VigenteHasta = request.VigenteHasta,
            Activa = true
        };

        _db.Reglas.Add(regla);
        await _db.SaveChangesAsync();

        return (true, null, ToDto(regla));
    }

    public async Task<ParametroEvaluacionDto?> ObtenerParametrosAsync(string rutaNormativa)
    {
        var parametros = await _db.ParametrosEvaluacion
            .AsNoTracking()
            .Where(p => p.RutaNormativa == rutaNormativa)
            .OrderByDescending(p => p.Version)
            .FirstOrDefaultAsync();

        return parametros is null ? null : ToDto(parametros);
    }

    public async Task<(bool Ok, string? Error, ParametroEvaluacionDto? Parametros)> ActualizarParametrosAsync(
        string rutaNormativa, ActualizarParametrosRequest request, string usuario)
    {
        if (!RutasNormativasValidas(rutaNormativa))
        {
            return (false, "La ruta normativa debe ser CAU-P-011 o CAU-P-122.", null);
        }

        // CA2: validación matemática antes de tocar la base de datos.
        var (valido, error) = ParametrosValidator.Validar(request.Theta, request.Delta, request.Tau);
        if (!valido)
        {
            return (false, error, null);
        }

        var actual = await _db.ParametrosEvaluacion
            .Where(p => p.RutaNormativa == rutaNormativa)
            .OrderByDescending(p => p.Version)
            .FirstOrDefaultAsync();

        if (actual is null)
        {
            actual = new ParametroEvaluacion { RutaNormativa = rutaNormativa, Version = 1 };
            _db.ParametrosEvaluacion.Add(actual);
        }
        else
        {
            actual.Version += 1;
        }

        actual.Theta = request.Theta;
        actual.Delta = request.Delta;
        actual.Tau = request.Tau;
        actual.ActualizadoUtc = DateTimeOffset.UtcNow;
        actual.ActualizadoPor = usuario;

        await _db.SaveChangesAsync();

        return (true, null, ToDto(actual));
    }

    private static bool RutasNormativasValidas(string ruta)
        => ruta is Siha.Contracts.RutasNormativas.CambioCarreraOu or Siha.Contracts.RutasNormativas.Movilidad;

    private static ReglaDto ToDto(Regla r) => new(
        r.Id, r.Codigo, r.RutaNormativa, r.TipoTramite, r.Descripcion, r.Expresion,
        r.Version, r.Fuente, r.EsNormativa, r.VigenteDesde, r.VigenteHasta, r.Activa);

    private static ParametroEvaluacionDto ToDto(ParametroEvaluacion p) => new(
        p.Id, p.RutaNormativa, p.Theta, p.Delta, p.Tau, p.Version, p.ActualizadoUtc, p.ActualizadoPor);
}
