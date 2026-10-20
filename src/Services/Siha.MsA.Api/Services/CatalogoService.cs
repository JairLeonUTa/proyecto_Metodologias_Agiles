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
            .Select(a => a.Codigo.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var importadas = 0;
        var rechazadas = 0;

        foreach (var item in request.Asignaturas)
        {
            if (item is null)
            {
                rechazadas++;
                errores.Add("Asignatura rechazada: la entrada no puede ser nula.");
                continue;
            }

            var unidades = item.Unidades ?? Array.Empty<UnidadTematicaRequest>();
            var (error, advertencias) = ValidacionCatalogo.Validar(
                item.Codigo, item.Nombre, item.Creditos, item.Horas, item.Semestre, unidades);
            if (error is not null)
            {
                rechazadas++;
                errores.Add($"Asignatura {item.Codigo} rechazada: {error}");
                continue;
            }

            var codigo = item.Codigo.Trim().ToUpperInvariant();
            if (codigosEnMalla.Contains(codigo))
            {
                rechazadas++;
                errores.Add($"Código duplicado en la malla {malla.Version}: {item.Codigo}.");
                continue;
            }

            var asignatura = new Asignatura
            {
                MallaId = malla.Id,
                Codigo = codigo,
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
                    Nombre = unidad.Nombre.Trim(),
                    Horas = unidad.Horas,
                    Contenidos = unidad.Contenidos ?? string.Empty
                });
            }

            asignatura.PlanCompleto = advertencias.Count == 0;
            errores.AddRange(advertencias.Select(a => $"Advertencia: {codigo} {a}; queda pendiente."));

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
