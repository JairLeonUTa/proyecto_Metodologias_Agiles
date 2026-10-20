using System.Data;
using Microsoft.EntityFrameworkCore;
using Siha.MsA.Api.Contracts.Dtos;
using Siha.MsA.Api.Data;
using Siha.MsA.Api.Domain.Entities;

namespace Siha.MsA.Api.Services;

public interface IReglasService
{
    Task<IReadOnlyCollection<ReglaDto>> ListarReglasAsync(string? rutaNormativa, string? tipoTramite);

    Task<IReadOnlyCollection<ReglaDto>> ListarHistorialReglasAsync(string? rutaNormativa, string? tipoTramite);

    Task<(bool Ok, string? Error, ReglaDto? Regla)> CrearReglaAsync(CrearReglaRequest request);

    Task<(bool Ok, string? Error, ReglaDto? Regla)> ActualizarReglaAsync(int id, CrearReglaRequest request);

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
        => await ConsultarReglasAsync(rutaNormativa, tipoTramite, soloVigentes: true);

    public async Task<IReadOnlyCollection<ReglaDto>> ListarHistorialReglasAsync(string? rutaNormativa, string? tipoTramite)
        => await ConsultarReglasAsync(rutaNormativa, tipoTramite, soloVigentes: false);

    private async Task<IReadOnlyCollection<ReglaDto>> ConsultarReglasAsync(
        string? rutaNormativa, string? tipoTramite, bool soloVigentes)
    {
        var query = _db.Reglas.AsNoTracking().AsQueryable();
        if (soloVigentes)
        {
            var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
            query = query.Where(r => r.Activa && r.VigenteDesde <= hoy &&
                (r.VigenteHasta == null || r.VigenteHasta >= hoy));
        }

        if (!string.IsNullOrWhiteSpace(rutaNormativa))
        {
            query = query.Where(r => r.RutaNormativa == rutaNormativa);
        }

        if (!string.IsNullOrWhiteSpace(tipoTramite))
        {
            query = query.Where(r => r.TipoTramite == tipoTramite);
        }

        return await query
            .OrderBy(r => r.RutaNormativa).ThenBy(r => r.Codigo).ThenBy(r => r.VigenteDesde).ThenBy(r => r.Id)
            .Select(r => ToDto(r))
            .ToListAsync();
    }

    public async Task<(bool Ok, string? Error, ReglaDto? Regla)> CrearReglaAsync(CrearReglaRequest request)
    {
        var error = ValidarRegla(request);
        if (error is not null) return (false, error, null);

        // SQL Server serializa las escrituras de una identidad sin cambiar el esquema.
        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;

        var duplicada = await _db.Reglas.AnyAsync(r =>
            r.Codigo == request.Codigo &&
            r.RutaNormativa == request.RutaNormativa);

        if (duplicada)
        {
            return (false, "Ya existe una regla con el mismo código y ruta normativa. Use PUT sobre su última versión.", null);
        }

        var regla = NuevaRegla(request);
        _db.Reglas.Add(regla);
        await _db.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();

        return (true, null, ToDto(regla));
    }

    public async Task<(bool Ok, string? Error, ReglaDto? Regla)> ActualizarReglaAsync(int id, CrearReglaRequest request)
    {
        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
        var actual = await _db.Reglas.FindAsync(id);
        // Error null distingue ausencia (404) de petición inválida (400).
        if (actual is null) return (false, null, null);

        var error = ValidarRegla(request);
        if (error is not null) return (false, error, null);
        if (request.Codigo != actual.Codigo || request.RutaNormativa != actual.RutaNormativa ||
            request.EsNormativa != actual.EsNormativa || request.TipoTramite != actual.TipoTramite)
            return (false, "No se puede cambiar código, ruta normativa, tipo de trámite ni naturaleza de la regla.", null);

        var versiones = _db.Reglas.Where(r => r.Codigo == actual.Codigo && r.RutaNormativa == actual.RutaNormativa);
        var ultimaId = await versiones.OrderByDescending(r => r.VigenteDesde).ThenByDescending(r => r.Id)
            .Select(r => r.Id).FirstAsync();
        if (!actual.Activa || ultimaId != id)
            return (false, "Solo se puede versionar la última regla activa de esta identidad.", null);
        if (await versiones.AnyAsync(r => r.Version == request.Version))
            return (false, "La versión debe ser distinta de todas las versiones históricas de la regla.", null);
        if (request.VigenteDesde <= actual.VigenteDesde)
            return (false, "La nueva vigencia debe comenzar después del inicio de la versión anterior.", null);

        // Fechas inclusivas: conserva la anterior para consultas históricas y evita solapamientos.
        var finAnterior = request.VigenteDesde.AddDays(-1);
        if (actual.VigenteHasta is null || actual.VigenteHasta > finAnterior)
            actual.VigenteHasta = finAnterior;
        var nueva = NuevaRegla(request);
        _db.Reglas.Add(nueva);
        await _db.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();
        return (true, null, ToDto(nueva));
    }

    private static Regla NuevaRegla(CrearReglaRequest request) => new()
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

    private static string? ValidarRegla(CrearReglaRequest request)
    {
        // Los atributos del record validan parámetros MVC, no propiedades de entidad.
        var campos = new (string? Valor, int Maximo)[]
        {
            (request.Codigo, 20), (request.RutaNormativa, 20), (request.TipoTramite, 20),
            (request.Descripcion, 400), (request.Version, 20), (request.Fuente, 200)
        };
        if (campos.Any(c => string.IsNullOrWhiteSpace(c.Valor) || c.Valor.Length > c.Maximo) ||
            request.Expresion?.Length > 400)
            return "Complete los campos obligatorios y respete las longitudes máximas del contrato.";
        if (!RutasNormativasValidas(request.RutaNormativa))
            return "La ruta normativa debe ser CAU-P-011 o CAU-P-122.";
        if (!Siha.Contracts.TiposTramite.Todos.Contains(request.TipoTramite))
            return "El tipo de trámite debe ser INTERNO o EXTERNO.";
        if (request.VigenteDesde == default || request.VigenteHasta < request.VigenteDesde)
            return "La vigencia debe tener un inicio válido y su fin no puede ser anterior al inicio.";
        return null;
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

        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
        var actual = await _db.ParametrosEvaluacion
            .Where(p => p.RutaNormativa == rutaNormativa)
            .OrderByDescending(p => p.Version)
            .FirstOrDefaultAsync();

        var nueva = new ParametroEvaluacion
        {
            RutaNormativa = rutaNormativa,
            Version = (actual?.Version ?? 0) + 1,
            Theta = request.Theta,
            Delta = request.Delta,
            Tau = request.Tau,
            ActualizadoUtc = DateTimeOffset.UtcNow,
            ActualizadoPor = usuario
        };
        _db.ParametrosEvaluacion.Add(nueva);

        await _db.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();

        return (true, null, ToDto(nueva));
    }

    private static bool RutasNormativasValidas(string ruta)
        => ruta is Siha.Contracts.RutasNormativas.CambioCarreraOu or Siha.Contracts.RutasNormativas.Movilidad;

    private static ReglaDto ToDto(Regla r) => new(
        r.Id, r.Codigo, r.RutaNormativa, r.TipoTramite, r.Descripcion, r.Expresion,
        r.Version, r.Fuente, r.EsNormativa, r.VigenteDesde, r.VigenteHasta, r.Activa);

    private static ParametroEvaluacionDto ToDto(ParametroEvaluacion p) => new(
        p.Id, p.RutaNormativa, p.Theta, p.Delta, p.Tau, p.Version, p.ActualizadoUtc, p.ActualizadoPor);
}
