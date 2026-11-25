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
        var decisiones = await _db.DecisionesFinales.AsNoTracking()
            .Where(d => d.ExpedienteId == expedienteId)
            .ToListAsync(cancellationToken);

        return Armar(contexto, dictamenes, esDocente && contexto.EsRevisor(correoUsuario), decisiones);
    }

    /// <summary>
    /// El administrador (coordinador o director de carrera) decide una materia en la que los dos docentes no se
    /// pusieron de acuerdo, con un comentario obligatorio. Solo se permite mientras la materia esté SIN_ACUERDO.
    /// </summary>
    public async Task<(SolicitudAnalisisResponse? Respuesta, string? Error, int Codigo)> DecidirComoAdministradorAsync(
        Guid expedienteId, string correo, string nombre, DecisionFinalRequest? solicitud, CancellationToken cancellationToken = default)
    {
        var contexto = await _db.AnalisisExpedientes.FirstOrDefaultAsync(a => a.ExpedienteId == expedienteId, cancellationToken);
        if (contexto is null)
            return (null, "La solicitud no existe.", StatusCodes.Status404NotFound);
        if (contexto.Estado != EstadosAnalisis.Analizado || contexto.ResultadoJson is null)
            return (null, "El agente todavía no termina el análisis de esta solicitud.", StatusCodes.Status409Conflict);

        var materia = solicitud?.CodigoDestino?.Trim() ?? string.Empty;
        var comentario = solicitud?.Comentario?.Trim() ?? string.Empty;
        if (comentario.Length == 0)
            return (null, "Escriba el motivo de la decisión final: queda registrado junto a la decisión.", StatusCodes.Status400BadRequest);

        var dictamenes = await _db.DictamenesDocentes.AsNoTracking().Where(d => d.ExpedienteId == expedienteId).ToListAsync(cancellationToken);
        var votacion = CalcularResultadoFinal(Leer(contexto.ResultadoJson), ARespuestas(dictamenes))
            .FirstOrDefault(m => string.Equals(m.CodigoDestino, materia, StringComparison.OrdinalIgnoreCase));
        if (votacion is null)
            return (null, $"La materia «{materia}» no forma parte de esta solicitud.", StatusCodes.Status400BadRequest);
        if (votacion.Resultado != SinAcuerdo)
            return (null, $"«{votacion.NombreDestino}» no necesita decisión final: los docentes no están en desacuerdo.", StatusCodes.Status409Conflict);

        var existente = await _db.DecisionesFinales
            .FirstOrDefaultAsync(d => d.ExpedienteId == expedienteId && d.Materia == votacion.CodigoDestino, cancellationToken);
        if (existente is null)
        {
            existente = new DecisionFinalAdministrador { ExpedienteId = expedienteId, Materia = votacion.CodigoDestino };
            _db.DecisionesFinales.Add(existente);
        }

        existente.Aprobada = solicitud!.Aprobada;
        existente.Comentario = comentario.Length > 2000 ? comentario[..2000] : comentario;
        existente.CorreoAdministrador = correo.ToLowerInvariant();
        existente.NombreAdministrador = nombre;
        existente.FechaUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await TablaResultadosHomologacion.GuardarAsync(_db, contexto, Leer(contexto.ResultadoJson), cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return (await ObtenerAsync(expedienteId, correo, esDocente: false, cancellationToken), null, StatusCodes.Status200OK);
    }

    /// <summary>Resultado de una materia en la que un docente aprobó y el otro no.</summary>
    public const string SinAcuerdo = "SIN_ACUERDO";

    /// <summary>Guarda (o reemplaza) la respuesta final del docente revisor que inicia sesión.</summary>
    public async Task<(SolicitudAnalisisResponse? Respuesta, string? Error, int Codigo)> ResponderAsync(
        Guid expedienteId, string correo, string nombre, DictamenRequest solicitud, CancellationToken cancellationToken = default)
    {
        var contexto = await _db.AnalisisExpedientes.FirstOrDefaultAsync(a => a.ExpedienteId == expedienteId, cancellationToken);
        if (contexto is null || !contexto.EsRevisor(correo))
            return (null, "La solicitud no existe o no está asignada a este docente.", StatusCodes.Status404NotFound);

        if (contexto.Estado != EstadosAnalisis.Analizado || contexto.ResultadoJson is null)
            return (null, "El agente todavía no termina el análisis de esta solicitud.", StatusCodes.Status409Conflict);

        var resultado = Leer(contexto.ResultadoJson);
        var bloqueadas = Todas(resultado).Where(m => m.Bloqueada).Select(m => m.Codigo).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var decisiones = (solicitud?.Decisiones ?? Array.Empty<DecisionMateriaRequest>())
            .GroupBy(d => d.CodigoDestino, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Last())
            .ToList();

        var noHomologables = decisiones.Where(d => bloqueadas.Contains(d.CodigoDestino)).Select(d => d.CodigoDestino).ToList();
        if (noHomologables.Count > 0)
            return (null, $"{string.Join(", ", noHomologables)}: no se puede(n) homologar porque los créditos no llegan al mínimo; no se vota(n).", StatusCodes.Status400BadRequest);

        var materias = Materias(resultado).Select(m => m.Codigo).ToHashSet(StringComparer.OrdinalIgnoreCase);

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

        // La decisión de los docentes también se refleja en la tabla RESULTADO_HOMOLOGACION.
        await TablaResultadosHomologacion.GuardarAsync(_db, contexto, Leer(contexto.ResultadoJson), cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return (await ObtenerAsync(expedienteId, correo, esDocente: true, cancellationToken), null, StatusCodes.Status200OK);
    }

    private static SolicitudAnalisisResponse Armar(
        AnalisisExpediente c, List<DictamenDocente> dictamenes, bool puedeResponder, IReadOnlyList<DecisionFinalAdministrador> decisionesFinales)
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
            : CalcularResultadoFinal(resultado, respuestas, decisionesFinales);

        var revisores = new[] { (c.Revisor1Nombre, c.Revisor1Correo), (c.Revisor2Nombre, c.Revisor2Correo) }
            .Where(r => !string.IsNullOrWhiteSpace(r.Item2))
            .Select(r => new RevisorAnalisisResponse(r.Item1, r.Item2,
                respuestas.Any(d => string.Equals(d.CorreoDocente, r.Item2, StringComparison.OrdinalIgnoreCase))))
            .ToList();

        var respondieron = revisores.Count(r => r.Respondio);
        // El agente solo recomienda: toda materia pedida la deciden los dos docentes.
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
            resultado, respuestas, final, estadoRevision, puedeResponder && c.Estado == EstadosAnalisis.Analizado,
            c.Estado is EstadosAnalisis.Analizado or EstadosAnalisis.Error && AnalisisAutomaticoService.EstaDesactualizado(c.ResultadoJson));
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
    /// rechazan, SIN_ACUERDO si discrepan y PENDIENTE mientras falte alguna respuesta. La recomendación del
    /// agente (CUMPLE o NO_CUMPLE) no decide: siempre votan los dos docentes.
    /// </summary>
    internal static List<ResultadoFinalMateriaResponse> CalcularResultadoFinal(
        AnalisisAutomaticoResponse resultado, IReadOnlyCollection<DictamenResponse> respuestas,
        IReadOnlyCollection<DecisionFinalAdministrador>? decisionesFinales = null)
        => Todas(resultado).Select(m =>
        {
            if (m.Bloqueada)
                return new ResultadoFinalMateriaResponse(m.Codigo, m.Nombre, m.Estimacion, 0, 0, "NO_HOMOLOGADA", true);
            var votos = respuestas
                .Select(r => r.Decisiones.FirstOrDefault(x => string.Equals(x.CodigoDestino, m.Codigo, StringComparison.OrdinalIgnoreCase)))
                .Where(x => x is not null)
                .ToList();
            var si = votos.Count(v => v!.Aprobada);
            var no = votos.Count - si;
            var estado = votos.Count < 2 ? "PENDIENTE" : si == 2 ? "HOMOLOGADA" : no == 2 ? "NO_HOMOLOGADA" : SinAcuerdo;

            // Sin acuerdo entre los docentes: decide el administrador (coordinador o director de carrera).
            var decision = estado == SinAcuerdo
                ? decisionesFinales?.FirstOrDefault(d => string.Equals(d.Materia, m.Codigo, StringComparison.OrdinalIgnoreCase))
                : null;
            if (decision is not null)
            {
                return new ResultadoFinalMateriaResponse(m.Codigo, m.Nombre, m.Estimacion, si, no,
                    decision.Aprobada ? "HOMOLOGADA" : "NO_HOMOLOGADA",
                    false,
                    new DecisionFinalResponse(decision.Aprobada, decision.Comentario, decision.NombreAdministrador, decision.FechaUtc));
            }

            return new ResultadoFinalMateriaResponse(m.Codigo, m.Nombre, m.Estimacion, si, no, estado, false);
        }).ToList();

    /// <summary>Todas las materias pedidas, con la estimación del agente.</summary>
    private static IEnumerable<(string Codigo, string Nombre, string Estimacion, bool Bloqueada)> Todas(AnalisisAutomaticoResponse r)
        => (r.Evaluaciones ?? Array.Empty<EvaluacionMateriaResponse>()).Select(e => (e.MateriaDestino, e.MateriaDestino, e.Estimacion, Bloqueada(e, r.Minimo)));

    /// <summary>
    /// Bloqueada por créditos. Los resultados guardados antes de existir <c>BloqueadaPorCreditos</c> no traen el dato,
    /// así que también se calcula con el porcentaje de créditos guardado (solo si existe: los muy antiguos no lo tienen).
    /// </summary>
    private static bool Bloqueada(EvaluacionMateriaResponse e, double? minimo)
        => e.BloqueadaPorCreditos
           || (e.Estimacion == AnalisisAutomaticoService.NoCumple && !e.SinSilabo
               && e.PorcentajeCreditos > 0 && (double)e.PorcentajeCreditos < (minimo ?? 80));

    /// <summary>Materias que los docentes deben decidir: todas las pedidas (el agente solo recomienda).</summary>
    private static IEnumerable<(string Codigo, string Nombre, string Estimacion)> Materias(AnalisisAutomaticoResponse r)
        => Todas(r).Where(m => !m.Bloqueada).Select(m => (m.Codigo, m.Nombre, m.Estimacion));

    /// <summary>Lee el resultado guardado y completa <c>BloqueadaPorCreditos</c> en los análisis anteriores a ese dato.</summary>
    private static AnalisisAutomaticoResponse Leer(string json)
    {
        var r = JsonSerializer.Deserialize<AnalisisAutomaticoResponse>(json, AnalisisAutomaticoService.Json)!;
        return r.Evaluaciones is null
            ? r
            : r with { Evaluaciones = r.Evaluaciones.Select(e => e with { BloqueadaPorCreditos = Bloqueada(e, r.Minimo) }).ToList() };
    }
}
