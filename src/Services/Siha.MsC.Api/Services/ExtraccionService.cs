using Microsoft.EntityFrameworkCore;
using Siha.MsC.Api.Contracts.Dtos;
using Siha.MsC.Api.Data;
using Siha.MsC.Api.Domain;
using Siha.MsC.Api.Domain.Entities;

namespace Siha.MsC.Api.Services;

public enum ResultadoConfirmacionExtraccion
{
    Confirmada,
    NoEncontrada,
    YaConfirmada,
    PendienteOcrLlm,
    EstadoNoPermitido,
    DatosInvalidos
}

public sealed record ConfirmacionExtraccionResultado(
    ResultadoConfirmacionExtraccion Resultado,
    Extraccion? Extraccion = null,
    IReadOnlyCollection<string>? Errores = null);

/// <summary>
/// Consulta y confirmación humana de extracciones (S2-J02, US-07).
///
/// Solo se confirma una extracción en <c>PENDIENTE_CONFIRMACION</c>. La confirmación exige
/// créditos, horas y al menos una unidad temática válidos; si algo falla, los datos originales
/// no se modifican. Una extracción ya confirmada o derivada a OCR/LLM no se puede confirmar aquí.
/// </summary>
public class ExtraccionService
{
    private readonly SihaDbContextC _db;

    public ExtraccionService(SihaDbContextC db)
    {
        _db = db;
    }

    public Task<Extraccion?> ObtenerAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.Extracciones
            .Include(e => e.Unidades)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<ConfirmacionExtraccionResultado> ConfirmarAsync(
        Guid id,
        ConfirmarExtraccionRequest? request,
        string usuario,
        CancellationToken cancellationToken = default)
    {
        var extraccion = await _db.Extracciones
            .Include(e => e.Unidades)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (extraccion is null)
            return new(ResultadoConfirmacionExtraccion.NoEncontrada);

        if (extraccion.Estado == EstadosExtraccion.Confirmada)
            return new(ResultadoConfirmacionExtraccion.YaConfirmada, extraccion);

        if (extraccion.Estado == EstadosExtraccion.PendienteOcrLlm)
            return new(ResultadoConfirmacionExtraccion.PendienteOcrLlm, extraccion);

        if (extraccion.Estado != EstadosExtraccion.PendienteConfirmacion)
            return new(ResultadoConfirmacionExtraccion.EstadoNoPermitido, extraccion);

        var errores = Validar(request);
        if (errores.Count > 0)
            return new(ResultadoConfirmacionExtraccion.DatosInvalidos, extraccion, errores);

        // request no es nulo: Validar lo garantiza.
        var confirmacion = request!;

        extraccion.Creditos = confirmacion.Creditos;
        extraccion.Horas = confirmacion.Horas;

        var existentes = extraccion.Unidades.ToDictionary(u => u.Numero);
        foreach (var unidad in confirmacion.Unidades!.OrderBy(u => u.Numero))
        {
            if (existentes.Remove(unidad.Numero, out var actual))
            {
                actual.Nombre = unidad.Nombre.Trim();
                actual.Horas = unidad.Horas;
                actual.Contenidos = unidad.Contenidos?.Trim() ?? string.Empty;
            }
            else
            {
                extraccion.Unidades.Add(new ExtraccionUnidad
                {
                    Numero = unidad.Numero,
                    Nombre = unidad.Nombre.Trim(),
                    Horas = unidad.Horas,
                    Contenidos = unidad.Contenidos?.Trim() ?? string.Empty
                });
            }
        }

        // Unidades que el operador eliminó al corregir el temario.
        foreach (var sobrante in existentes.Values)
            extraccion.Unidades.Remove(sobrante);

        extraccion.Estado = EstadosExtraccion.Confirmada;
        extraccion.ConfirmadoUtc = DateTimeOffset.UtcNow;
        extraccion.ConfirmadoPor = usuario;
        extraccion.Observacion = string.IsNullOrWhiteSpace(confirmacion.Observacion)
            ? null
            : confirmacion.Observacion.Trim();

        await _db.SaveChangesAsync(cancellationToken);

        return new(ResultadoConfirmacionExtraccion.Confirmada, extraccion);
    }

    public static IReadOnlyCollection<string> Validar(ConfirmarExtraccionRequest? request)
    {
        var errores = new List<string>();

        if (request is null)
        {
            errores.Add("El cuerpo de la confirmación es obligatorio.");
            return errores;
        }

        if (request.Creditos is < 1 or > 30)
            errores.Add("Los créditos deben estar en el rango 1–30.");

        if (request.Horas is < 1 or > 2000)
            errores.Add("Las horas deben estar en el rango 1–2000.");

        var unidades = request.Unidades;
        if (unidades is null || unidades.Count == 0)
        {
            errores.Add("Debe incluir al menos una unidad temática.");
            return errores;
        }

        if (unidades.Any(u => u is null ||
            u.Numero is < 1 or > 50 ||
            string.IsNullOrWhiteSpace(u.Nombre) || u.Nombre.Trim().Length > 200 ||
            u.Horas is < 1 or > 1000 ||
            (u.Contenidos?.Length ?? 0) > 4000))
        {
            errores.Add("Cada unidad requiere número (1–50), nombre (máximo 200), horas (1–1000) y contenidos de máximo 4000 caracteres.");
        }

        if (unidades.Where(u => u is not null).GroupBy(u => u.Numero).Any(g => g.Count() > 1))
            errores.Add("No se permiten números de unidad duplicados.");

        return errores;
    }
}
