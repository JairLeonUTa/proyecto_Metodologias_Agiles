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
    public const string Cumple = AnalisisAutomaticoService.Cumple;
    public const string CriterioDocente = AnalisisAutomaticoService.CriterioDocente;

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

        var noCumplen = Todas(Leer(contexto.ResultadoJson))
            .Where(m => m.Estimacion == AnalisisAutomaticoService.NoCumple)
            .Select(m => m.Codigo)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var noHomologables = decisiones.Where(d => noCumplen.Contains(d.CodigoDestino)).Select(d => d.CodigoDestino).ToList();
        if (noHomologables.Count > 0)
            return (null, $"{string.Join(", ", noHomologables)}: no se puede(n) homologar porque no llega(n) al 80 % en contenidos o en créditos; no se vota(n).",
                StatusCodes.Status400BadRequest);

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

        // La decisión de los docentes también se refleja en la tabla RESULTADO_HOMOLOGACION.
        await TablaResultadosHomologacion.GuardarAsync(_db, contexto, Leer(contexto.ResultadoJson), cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return (await ObtenerAsync(expedienteId, correo, esDocente: true, cancellationToken), null, StatusCodes.Status200OK);
    }

    private static SolicitudAnalisisResponse Armar(AnalisisExpediente c, List<DictamenDocente> dictamenes, bool puedeResponder)
    {
        AnalisisAutomaticoResponse? resultado = null;
        try
        {
            resultado = c.ResultadoJson is null ? null : Leer(c.ResultadoJson);
            if (resultado?.Evaluaciones is null) resultado = null;
        }
        catch (JsonException)
        {
            // Resultado del formato anterior: se debe volver a analizar.
        }
        var respuestas = ARespuestas(dictamenes);
        var final = resultado is null
            ? new List<ResultadoFinalMateriaResponse>()
            : CalcularResultadoFinal(resultado, respuestas);

        var revisores = new[] { (c.Revisor1Nombre, c.Revisor1Correo), (c.Revisor2Nombre, c.Revisor2Correo) }
            .Where(r => !string.IsNullOrWhiteSpace(r.Item2))
            .Select(r => new RevisorAnalisisResponse(r.Item1, r.Item2,
                respuestas.Any(d => string.Equals(d.CorreoDocente, r.Item2, StringComparison.OrdinalIgnoreCase))))
            .ToList();

        var respondieron = revisores.Count(r => r.Respondio);
        // Si ninguna materia necesita la decisión de los docentes (todas quedaron NO_HOMOLOGADA por el
        // reglamento), la solicitud ya está resuelta y no hay nada que responder.
        var nadaQueDecidir = resultado is not null && !Materias(resultado).Any();
        var estadoRevision = nadaQueDecidir ? "RESUELTA"
            : respondieron == 0 ? "PENDIENTE" : respondieron < 2 ? "EN_REVISION" : "RESUELTA";
        puedeResponder = puedeResponder && !nadaQueDecidir;

        Siha.Certificados.CertificadoSiha? certificado = null;
        try
        {
            certificado = JsonSerializer.Deserialize<Siha.Certificados.CertificadoSiha>(c.CertificadoJson);
        }
        catch (JsonException)
        {
            // Solicitudes registradas con el formato anterior del certificado: sin historial.
        }
        var historial = (certificado?.Materias ?? (IReadOnlyList<Siha.Certificados.MateriaCursada>)Array.Empty<Siha.Certificados.MateriaCursada>())
            .OrderBy(m => m.Nivel).ThenBy(m => m.Nombre)
            .Select(m => new MateriaHistorialResponse(m.Nombre, m.Nivel, m.PeriodoAcademico, m.Nota, m.Aprobada))
            .ToList();

        return new SolicitudAnalisisResponse(
            c.ExpedienteId, c.CodigoExpediente, c.NumeroCertificado, c.EstudianteNombre, c.TipoTramite, c.RutaNormativa,
            c.InstitucionOrigen, c.CarreraOrigenNombre, c.CarreraDestinoNombre, historial, revisores, c.Estado, c.Mensaje, c.AnalizadoUtc,
            resultado, respuestas, final, estadoRevision, puedeResponder && c.Estado == EstadosAnalisis.Analizado);
    }

    internal static List<DictamenResponse> ARespuestas(IEnumerable<DictamenDocente> dictamenes)
        => dictamenes.Select(d => new DictamenResponse(
            d.CorreoDocente,
            d.NombreDocente,
            JsonSerializer.Deserialize<List<DecisionMateriaRequest>>(d.DecisionesJson, AnalisisAutomaticoService.Json) ?? new(),
            d.Comentario,
            d.FechaUtc)).ToList();

    /// <summary>
    /// Resultado final por materia: HOMOLOGADA si los dos docentes aprueban, NO_HOMOLOGADA si los dos
    /// rechazan, SIN_ACUERDO si discrepan y PENDIENTE mientras falte alguna respuesta. Las que no llegan al
    /// 80 % en contenidos o créditos (NO_CUMPLE) quedan NO_HOMOLOGADA directamente: el reglamento no las
    /// permite y los docentes no las votan.
    /// </summary>
    internal static List<ResultadoFinalMateriaResponse> CalcularResultadoFinal(
        AnalisisAutomaticoResponse resultado, IReadOnlyCollection<DictamenResponse> respuestas)
        => Todas(resultado).Select(m =>
        {
            if (m.Estimacion == AnalisisAutomaticoService.NoCumple)
                return new ResultadoFinalMateriaResponse(m.Codigo, m.Nombre, m.Estimacion, 0, 0, "NO_HOMOLOGADA");

            var votos = respuestas
                .Select(r => r.Decisiones.FirstOrDefault(x => string.Equals(x.CodigoDestino, m.Codigo, StringComparison.OrdinalIgnoreCase)))
                .Where(x => x is not null)
                .ToList();
            var si = votos.Count(v => v!.Aprobada);
            var no = votos.Count - si;
            var estado = votos.Count < 2 ? "PENDIENTE" : si == 2 ? "HOMOLOGADA" : no == 2 ? "NO_HOMOLOGADA" : "SIN_ACUERDO";
            return new ResultadoFinalMateriaResponse(m.Codigo, m.Nombre, m.Estimacion, si, no, estado);
        }).ToList();

    /// <summary>Todas las materias pedidas, con la estimación del agente.</summary>
    private static IEnumerable<(string Codigo, string Nombre, string Estimacion)> Todas(AnalisisAutomaticoResponse r)
        => (r.Evaluaciones ?? Array.Empty<EvaluacionMateriaResponse>()).Select(e => (e.MateriaDestino, e.MateriaDestino, e.Estimacion));

    /// <summary>
    /// Materias que los docentes deben decidir: las que cumplen (confirman) y las que no tienen sílabo
    /// (deciden con la documentación). Las que no llegan al 80 % no se votan: no se pueden homologar.
    /// </summary>
    private static IEnumerable<(string Codigo, string Nombre, string Estimacion)> Materias(AnalisisAutomaticoResponse r)
        => Todas(r).Where(m => m.Estimacion != AnalisisAutomaticoService.NoCumple);

    private static AnalisisAutomaticoResponse Leer(string json)
        => JsonSerializer.Deserialize<AnalisisAutomaticoResponse>(json, AnalisisAutomaticoService.Json)!;
}
