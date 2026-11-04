using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Siha.MsC.Api.Contracts.Dtos;
using Siha.MsC.Api.Data;
using Siha.MsC.Api.Domain.Entities;

namespace Siha.MsC.Api.Services;

/// <summary>
/// Consulta de la solicitud analizada y respuesta final de los dos docentes revisores.
/// El agente estima (≥ 80 % cumple; menos, a criterio del docente) y cada revisor decide materia por materia
/// si está de acuerdo. Una materia queda HOMOLOGADA cuando los dos la aprueban.
/// </summary>
public sealed class SolicitudesAnalisisService
{
    public const string Cumple = "CUMPLE";
    public const string CriterioDocente = "CRITERIO_DOCENTE";

    private readonly SihaDbContextC _db;

    public SolicitudesAnalisisService(SihaDbContextC db) => _db = db;

    public async Task<SolicitudAnalisisResponse?> ObtenerAsync(
        Guid expedienteId, string? correoUsuario, bool esDocente, CancellationToken cancellationToken = default)
    {
        var contexto = await _db.AnalisisExpedientes.AsNoTracking()
            .FirstOrDefaultAsync(a => a.ExpedienteId == expedienteId, cancellationToken);

        // Un docente solo ve las solicitudes que le asignaron.
        if (contexto is null || (esDocente && !contexto.EsRevisor(correoUsuario)))
        {
            return null;
        }

        var dictamenes = await _db.DictamenesDocentes.AsNoTracking()
            .Where(d => d.ExpedienteId == expedienteId)
            .OrderBy(d => d.FechaUtc)
            .ToListAsync(cancellationToken);

        return Armar(contexto, dictamenes, esDocente && contexto.EsRevisor(correoUsuario));
    }

    /// <summary>Guarda (o reemplaza) la respuesta final del docente revisor que inicia sesión.</summary>
    public async Task<(SolicitudAnalisisResponse? Respuesta, string? Error, int Codigo)> ResponderAsync(
        Guid expedienteId, string correo, string nombre, DictamenRequest solicitud, CancellationToken cancellationToken = default)
    {
        var contexto = await _db.AnalisisExpedientes.FirstOrDefaultAsync(a => a.ExpedienteId == expedienteId, cancellationToken);
        if (contexto is null || !contexto.EsRevisor(correo))
            return (null, "La solicitud no existe o no está asignada a este docente.", StatusCodes.Status404NotFound);

        if (contexto.Estado != EstadosAnalisis.Analizado || contexto.ResultadoJson is null)
            return (null, "El agente todavía no termina el análisis de esta solicitud.", StatusCodes.Status409Conflict);

        var materias = Materias(Leer(contexto.ResultadoJson)).Select(m => m.Codigo).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var decisiones = (solicitud?.Decisiones ?? Array.Empty<DecisionMateriaRequest>())
            .GroupBy(d => d.CodigoDestino, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Last())
            .ToList();

        var faltan = materias.Where(m => !decisiones.Any(d => string.Equals(d.CodigoDestino, m, StringComparison.OrdinalIgnoreCase))).ToList();
        if (faltan.Count > 0)
            return (null, $"Debe decidir todas las materias. Falta: {string.Join(", ", faltan)}.", StatusCodes.Status400BadRequest);

        var ajenas = decisiones.Where(d => !materias.Contains(d.CodigoDestino)).Select(d => d.CodigoDestino).ToList();
        if (ajenas.Count > 0)
            return (null, $"Las materias {string.Join(", ", ajenas)} no forman parte del análisis.", StatusCodes.Status400BadRequest);

        var existente = await _db.DictamenesDocentes
            .FirstOrDefaultAsync(d => d.ExpedienteId == expedienteId && d.CorreoDocente == correo.ToLower(), cancellationToken);
        if (existente is null)
        {
            existente = new DictamenDocente { ExpedienteId = expedienteId, CorreoDocente = correo.ToLowerInvariant() };
            _db.DictamenesDocentes.Add(existente);
        }

        existente.NombreDocente = nombre;
        existente.DecisionesJson = JsonSerializer.Serialize(decisiones, AnalisisAutomaticoService.Json);
        existente.Comentario = string.IsNullOrWhiteSpace(solicitud?.Comentario) ? null : solicitud.Comentario.Trim();
        existente.FechaUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return (await ObtenerAsync(expedienteId, correo, esDocente: true, cancellationToken), null, StatusCodes.Status200OK);
    }

    private static SolicitudAnalisisResponse Armar(AnalisisExpediente c, List<DictamenDocente> dictamenes, bool puedeResponder)
    {
        var resultado = c.ResultadoJson is null ? null : Leer(c.ResultadoJson);
        var respuestas = dictamenes.Select(d => new DictamenResponse(
            d.CorreoDocente,
            d.NombreDocente,
            JsonSerializer.Deserialize<List<DecisionMateriaRequest>>(d.DecisionesJson, AnalisisAutomaticoService.Json) ?? new(),
            d.Comentario,
            d.FechaUtc)).ToList();

        var final = resultado is null
            ? new List<ResultadoFinalMateriaResponse>()
            : Materias(resultado).Select(m =>
            {
                var votos = respuestas
                    .Select(r => r.Decisiones.FirstOrDefault(x => string.Equals(x.CodigoDestino, m.Codigo, StringComparison.OrdinalIgnoreCase)))
                    .Where(x => x is not null)
                    .ToList();
                var si = votos.Count(v => v!.Aprobada);
                var no = votos.Count - si;
                var estado = votos.Count < 2 ? "PENDIENTE" : si == 2 ? "HOMOLOGADA" : no == 2 ? "NO_HOMOLOGADA" : "SIN_ACUERDO";
                return new ResultadoFinalMateriaResponse(m.Codigo, m.Nombre, m.Estimacion, si, no, estado);
            }).ToList();

        var revisores = new[] { (c.Revisor1Nombre, c.Revisor1Correo), (c.Revisor2Nombre, c.Revisor2Correo) }
            .Where(r => !string.IsNullOrWhiteSpace(r.Item2))
            .Select(r => new RevisorAnalisisResponse(r.Item1, r.Item2,
                respuestas.Any(d => string.Equals(d.CorreoDocente, r.Item2, StringComparison.OrdinalIgnoreCase))))
            .ToList();

        var respondieron = revisores.Count(r => r.Respondio);
        var estadoRevision = respondieron == 0 ? "PENDIENTE" : respondieron < 2 ? "EN_REVISION" : "RESUELTA";

        return new SolicitudAnalisisResponse(
            c.ExpedienteId, c.CodigoExpediente, c.NumeroCertificado, c.EstudianteNombre, c.TipoTramite, c.RutaNormativa,
            c.InstitucionOrigen, c.CarreraOrigenCodigo, c.CarreraDestinoCodigo, revisores, c.Estado, c.Mensaje, c.AnalizadoUtc,
            resultado, respuestas, final, estadoRevision, puedeResponder && c.Estado == EstadosAnalisis.Analizado);
    }

    /// <summary>Materias que los docentes deben decidir: las que cumplen y las que quedan a su criterio.</summary>
    private static IEnumerable<(string Codigo, string Nombre, string Estimacion)> Materias(AnalisisAutomaticoResponse r)
        => r.Propuestas.Select(p => (p.CodigoDestino, p.NombreDestino, Cumple))
            .Concat(r.SinCoberturaSuficiente.Select(p => (p.CodigoDestino, p.NombreDestino, CriterioDocente)));

    private static AnalisisAutomaticoResponse Leer(string json)
        => JsonSerializer.Deserialize<AnalisisAutomaticoResponse>(json, AnalisisAutomaticoService.Json)!;
}
