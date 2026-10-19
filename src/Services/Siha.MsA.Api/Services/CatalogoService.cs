using Microsoft.EntityFrameworkCore;
using Siha.MsA.Api.Contracts.Dtos;
using Siha.MsA.Api.Data;
using Siha.MsA.Api.Domain.Entities;

namespace Siha.MsA.Api.Services;

public interface ICatalogoService
{
    Task<(bool Ok, string? Error, ImportacionResultadoDto? Resultado)> ImportarAsync(ImportarCatalogoRequest request);
}

/// <summary>
/// Carga masiva del catálogo académico estructurado (US-03, tarea S1-J05).
/// Valida códigos duplicados (rechazo), equivalencia de 48 h por crédito UTA y marca como
/// pendientes los planes incompletos, de modo que no pasen a análisis semántico.
/// </summary>
public class CatalogoService : ICatalogoService
{
    public const int HorasPorCreditoUta = 48;

    private readonly SihaDbContextA _db;

    public CatalogoService(SihaDbContextA db)
    {
        _db = db;
    }

    public async Task<(bool Ok, string? Error, ImportacionResultadoDto? Resultado)> ImportarAsync(
        ImportarCatalogoRequest request)
    {
        if (request.Asignaturas is null || request.Asignaturas.Count == 0)
        {
            return (false, "Debe enviar al menos una asignatura para la importación.", null);
        }

        var malla = await _db.Mallas
            .Include(m => m.Asignaturas)
            .FirstOrDefaultAsync(m => m.Id == request.MallaId);

        if (malla is null)
        {
            return (false, $"La malla {request.MallaId} no existe en el catálogo.", null);
        }

        var errores = new List<string>();
        var codigosEnMalla = malla.Asignaturas
            .Select(a => a.Codigo)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var importadas = 0;
        var rechazadas = 0;

        foreach (var item in request.Asignaturas)
        {
            if (string.IsNullOrWhiteSpace(item.Codigo) || string.IsNullOrWhiteSpace(item.Nombre))
            {
                rechazadas++;
                errores.Add("Asignatura rechazada: el código y el nombre son obligatorios.");
                continue;
            }

            if (codigosEnMalla.Contains(item.Codigo))
            {
                rechazadas++;
                errores.Add($"Código duplicado en la malla {malla.Version}: {item.Codigo}.");
                continue;
            }

            var unidades = item.Unidades ?? Array.Empty<UnidadTematicaRequest>();
            var numerosDuplicados = unidades
                .GroupBy(u => u.Numero)
                .Any(g => g.Count() > 1);

            var asignatura = new Asignatura
            {
                MallaId = malla.Id,
                Codigo = item.Codigo.Trim(),
                Nombre = item.Nombre.Trim(),
                Creditos = item.Creditos,
                Horas = item.Horas,
                Semestre = item.Semestre
            };

            foreach (var unidad in unidades.OrderBy(u => u.Numero))
            {
                asignatura.Unidades.Add(new UnidadTematica
                {
                    Numero = unidad.Numero,
                    Nombre = unidad.Nombre,
                    Horas = unidad.Horas,
                    Contenidos = unidad.Contenidos ?? string.Empty
                });
            }

            var completo = true;

            if (item.Creditos <= 0)
            {
                completo = false;
                errores.Add($"Advertencia: {item.Codigo} no declara créditos válidos; queda pendiente.");
            }
            else if (item.Horas != item.Creditos * HorasPorCreditoUta)
            {
                completo = false;
                errores.Add(
                    $"Advertencia: {item.Codigo} no cumple la equivalencia de {HorasPorCreditoUta} h/crédito UTA " +
                    $"(esperado {item.Creditos * HorasPorCreditoUta} h, recibido {item.Horas} h); queda pendiente.");
            }

            if (unidades.Count == 0)
            {
                completo = false;
                errores.Add($"Advertencia: {item.Codigo} no tiene unidades temáticas; queda pendiente.");
            }

            if (numerosDuplicados)
            {
                completo = false;
                errores.Add($"Advertencia: {item.Codigo} repite el número de unidad; queda pendiente.");
            }

            var horasUnidades = unidades.Sum(u => (long)u.Horas);
            if (horasUnidades > item.Horas)
            {
                completo = false;
                errores.Add(
                    $"Advertencia: {item.Codigo} suma {horasUnidades} h en unidades y supera las {item.Horas} h declaradas; queda pendiente.");
            }

            asignatura.PlanCompleto = completo;

            _db.Asignaturas.Add(asignatura);
            codigosEnMalla.Add(asignatura.Codigo);
            importadas++;
        }

        await _db.SaveChangesAsync();

        var resultado = new ImportacionResultadoDto(
            request.Asignaturas.Count, importadas, rechazadas, errores);

        return (true, null, resultado);
    }
}
