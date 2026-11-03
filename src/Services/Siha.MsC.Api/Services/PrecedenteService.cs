using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Siha.MsC.Api.Contracts.Dtos;
using Siha.MsC.Api.Data;
using Siha.MsC.Api.Domain.Entities;

namespace Siha.MsC.Api.Services;

/// <summary>
/// Implementación de búsqueda de precedentes en DB C.
/// Coincidencia EXACTA: mismos códigos origen (conjunto), mismo destino, misma institución.
/// Sin coincidencia → lista vacía (NO inventa "parecidos").
/// </summary>
public sealed class PrecedenteService : IPrecedenteService
{
    private readonly SihaDbContextC _db;

    public PrecedenteService(SihaDbContextC db)
    {
        _db = db;
    }

    public async Task<IReadOnlyCollection<PrecedenteResponse>> BuscarAsync(
        BuscarPrecedenteRequest request,
        CancellationToken cancellationToken = default)
    {
        var codigosOrigen = request.CodigosAsignaturasOrigen
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant())
            .OrderBy(c => c)
            .ToList();

        if (codigosOrigen.Count == 0)
            return Array.Empty<PrecedenteResponse>();

        var destino = request.CodigoAsignaturaDestino?.Trim().ToUpperInvariant() ?? string.Empty;
        var institucion = request.InstitucionOrigen?.Trim().ToUpperInvariant() ?? string.Empty;

        // Normalizar JSON de búsqueda para comparar
        var origenJsonBuscado = JsonSerializer.Serialize(codigosOrigen);

        var candidatos = await _db.Precedentes
            .AsNoTracking()
            .Where(p => p.AsignaturaDestino.ToUpper() == destino
                     && p.InstitucionOrigen.ToUpper() == institucion)
            .ToListAsync(cancellationToken);

        var resultados = candidatos
            .Where(p => NormalizarJsonAsignaturas(p.AsignaturasOrigenJson) == origenJsonBuscado)
            .Select(p => new PrecedenteResponse(
                p.Id,
                p.Resolucion,
                DeserializarAsignaturasOrigen(p.AsignaturasOrigenJson),
                p.AsignaturaDestino,
                p.InstitucionOrigen,
                p.Fecha))
            .ToList();

        return resultados.AsReadOnly();
    }

    public async Task<Guid> RegistrarAsync(
        string resolucion,
        IReadOnlyCollection<(string Codigo, string Nombre)> asignaturasOrigen,
        string asignaturaDestino,
        string institucionOrigen,
        DateOnly fecha,
        CancellationToken cancellationToken = default)
    {
        var origenJson = JsonSerializer.Serialize(
            asignaturasOrigen
                .Select(a => new { Codigo = a.Codigo.Trim().ToUpperInvariant(), Nombre = a.Nombre.Trim() })
                .OrderBy(a => a.Codigo)
                .ToList());

        var precedente = new Precedente
        {
            Resolucion = resolucion.Trim(),
            AsignaturasOrigenJson = origenJson,
            AsignaturaDestino = asignaturaDestino.Trim().ToUpperInvariant(),
            InstitucionOrigen = institucionOrigen.Trim().ToUpperInvariant(),
            Fecha = fecha
        };

        _db.Precedentes.Add(precedente);
        await _db.SaveChangesAsync(cancellationToken);

        return precedente.Id;
    }

    private static string NormalizarJsonAsignaturas(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return "[]";

        try
        {
            using var doc = JsonDocument.Parse(json);
            var codigos = doc.RootElement
                .EnumerateArray()
                .Select(e => e.GetProperty("Codigo").GetString()?.Trim().ToUpperInvariant() ?? string.Empty)
                .Where(c => !string.IsNullOrEmpty(c))
                .OrderBy(c => c)
                .ToList();
            return JsonSerializer.Serialize(codigos);
        }
        catch
        {
            return "[]";
        }
    }

    private static IReadOnlyCollection<PrecedenteAsignaturaOrigenResponse> DeserializarAsignaturasOrigen(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<PrecedenteAsignaturaOrigenResponse>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement
                .EnumerateArray()
                .Select(e => new PrecedenteAsignaturaOrigenResponse(
                    e.GetProperty("Codigo").GetString() ?? string.Empty,
                    e.GetProperty("Nombre").GetString() ?? string.Empty))
                .ToList()
                .AsReadOnly();
        }
        catch
        {
            return Array.Empty<PrecedenteAsignaturaOrigenResponse>();
        }
    }
}