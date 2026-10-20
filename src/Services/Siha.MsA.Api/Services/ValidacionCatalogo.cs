using Siha.MsA.Api.Contracts.Dtos;
using Siha.MsA.Api.Domain.Entities;

namespace Siha.MsA.Api.Services;

/// <summary>La misma política para alta, edición, importación y lectura de históricos.</summary>
public static class ValidacionCatalogo
{
    public static (string? Error, IReadOnlyCollection<string> Advertencias) Validar(
        string codigo, string nombre, int creditos, int horas, int semestre,
        IReadOnlyCollection<UnidadTematicaRequest> unidades)
    {
        if (string.IsNullOrWhiteSpace(codigo) || codigo.Trim().Length > 30 ||
            string.IsNullOrWhiteSpace(nombre) || nombre.Trim().Length > 200)
            return ("El código (máximo 30) y el nombre (máximo 200) son obligatorios.", Array.Empty<string>());
        if (creditos is < 1 or > 30 || horas is < 1 or > 2000 || semestre is < 1 or > 20)
            return ("Créditos (1–30), horas (1–2000) y semestre (1–20) deben ser válidos.", Array.Empty<string>());
        if (unidades.Any(u => u is null || u.Numero is < 1 or > 50 || u.Horas is < 1 or > 1000 ||
            string.IsNullOrWhiteSpace(u.Nombre) || u.Nombre.Trim().Length > 200 || (u.Contenidos?.Length ?? 0) > 4000))
            return ("Las unidades requieren número (1–50), nombre (máximo 200), horas (1–1000) y contenidos de máximo 4000 caracteres.", Array.Empty<string>());
        if (unidades.GroupBy(u => u.Numero).Any(g => g.Count() > 1))
            return ("No se permiten números de unidad duplicados.", Array.Empty<string>());

        var advertencias = new List<string>();
        if (horas != (long)creditos * CatalogoService.HorasPorCreditoUta)
            advertencias.Add($"no cumple la equivalencia de {CatalogoService.HorasPorCreditoUta} h/crédito UTA");
        if (unidades.Count == 0)
            advertencias.Add("no tiene unidades temáticas");
        else if (unidades.Sum(u => (long)u.Horas) != horas)
            advertencias.Add($"la suma de horas de unidades no coincide con las {horas} h declaradas");
        return (null, advertencias);
    }

    public static (string? Error, IReadOnlyCollection<string> Advertencias) Evaluar(Asignatura a) =>
        Validar(a.Codigo, a.Nombre, a.Creditos, a.Horas, a.Semestre,
            a.Unidades.Select(u => new UnidadTematicaRequest(u.Numero, u.Nombre, u.Horas, u.Contenidos)).ToArray());
}
