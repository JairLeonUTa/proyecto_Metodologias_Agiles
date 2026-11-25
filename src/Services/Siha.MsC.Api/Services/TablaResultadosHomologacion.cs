using Microsoft.EntityFrameworkCore;
using Siha.MsC.Api.Contracts.Dtos;
using Siha.MsC.Api.Data;
using Siha.MsC.Api.Domain.Entities;

namespace Siha.MsC.Api.Services;

/// <summary>
/// Mantiene la tabla RESULTADO_HOMOLOGACION: una fila por cada materia pedida, con los porcentajes del
/// agente (contenidos, profundidad, carga horaria y créditos) y la decisión de los dos docentes.
///
/// Se llama cuando el agente termina un análisis y cada vez que un docente responde. Las filas se
/// actualizan en su lugar (por número de solicitud) para no chocar con el índice único. No guarda nada
/// por sí misma: el llamador hace <c>SaveChanges</c>.
/// </summary>
public static class TablaResultadosHomologacion
{
    public static async Task GuardarAsync(
        SihaDbContextC db, AnalisisExpediente contexto, AnalisisAutomaticoResponse resultado, CancellationToken cancellationToken)
    {
        var evaluaciones = resultado.Evaluaciones ?? Array.Empty<EvaluacionMateriaResponse>();
        var dictamenes = await db.DictamenesDocentes.AsNoTracking()
            .Where(d => d.ExpedienteId == contexto.ExpedienteId)
            .ToListAsync(cancellationToken);
        var decisiones = await db.DecisionesFinales.AsNoTracking()
            .Where(d => d.ExpedienteId == contexto.ExpedienteId)
            .ToListAsync(cancellationToken);
        var finales = SolicitudesAnalisisService.CalcularResultadoFinal(resultado, SolicitudesAnalisisService.ARespuestas(dictamenes), decisiones);

        var filas = await db.ResultadosHomologacion
            .Where(r => r.ExpedienteId == contexto.ExpedienteId)
            .ToListAsync(cancellationToken);

        // Si un nuevo análisis ya no trae alguna solicitud, su fila se elimina.
        var numeros = evaluaciones.Select(e => e.Numero).ToHashSet();
        db.ResultadosHomologacion.RemoveRange(filas.Where(f => !numeros.Contains(f.NumeroSolicitud)));

        var ahora = DateTimeOffset.UtcNow;
        foreach (var e in evaluaciones)
        {
            var fila = filas.FirstOrDefault(f => f.NumeroSolicitud == e.Numero);
            if (fila is null)
            {
                fila = new ResultadoHomologacion { ExpedienteId = contexto.ExpedienteId, NumeroSolicitud = e.Numero };
                db.ResultadosHomologacion.Add(fila);
            }

            var final = finales.FirstOrDefault(f => string.Equals(f.CodigoDestino, e.MateriaDestino, StringComparison.OrdinalIgnoreCase));
            Llenar(fila, contexto, resultado, e, final, ahora);
        }
    }

    internal static void Llenar(
        ResultadoHomologacion fila,
        AnalisisExpediente c,
        AnalisisAutomaticoResponse resultado,
        EvaluacionMateriaResponse e,
        ResultadoFinalMateriaResponse? final,
        DateTimeOffset ahora)
    {
        var origenes = e.MateriasOrigen ?? Array.Empty<MateriaOrigenEvaluadaResponse>();

        fila.CodigoExpediente = Recortar(c.CodigoExpediente, 30);
        fila.NumeroCertificado = Recortar(c.NumeroCertificado, 40);
        fila.EstudianteNombre = Recortar(c.EstudianteNombre, 200);
        fila.TipoTramite = Recortar(c.TipoTramite, 20);
        fila.RutaNormativa = Recortar(c.RutaNormativa, 20);
        fila.InstitucionOrigen = Recortar(c.InstitucionOrigen, 150);
        fila.CarreraOrigen = Recortar(c.CarreraOrigenNombre, 150);
        fila.CarreraDestino = Recortar(c.CarreraDestinoNombre, 150);
        fila.MateriasOrigen = Recortar(string.Join(" + ", origenes.Select(o => o.Nombre)), 600);
        fila.MateriaDestino = Recortar(e.MateriaDestino, 200);
        fila.CodigoDestino = e.CodigoDestino is null ? null : Recortar(e.CodigoDestino, 30);

        // Créditos: solo si se conocen los de todas las materias presentadas.
        fila.CreditosOrigen = e.CreditosOrigen ?? (origenes.Count > 0 && origenes.All(o => o.Creditos is > 0)
            ? origenes.Sum(o => o.Creditos!.Value)
            : null);
        fila.CreditosDestino = e.CreditosDestino is > 0 ? e.CreditosDestino : null;
        // El mismo porcentaje con el que decidió el agente (con las horas si no se conocían los créditos).
        // Sin sílabo no se pudo medir.
        // (Los análisis guardados antes de la regla de créditos no traen el porcentaje: se calcula aquí.)
        fila.PorcentajeCreditos = e.SinSilabo
            ? null
            : e.PorcentajeCreditos > 0
                ? e.PorcentajeCreditos
                : fila.CreditosOrigen is { } co && fila.CreditosDestino is { } cd
                    ? Math.Min(100m, Math.Round((decimal)co / cd * 100m, 2))
                    : e.PorcentajeCreditos;

        fila.HorasOrigen = origenes.Count > 0 && origenes.All(o => o.Horas is > 0)
            ? origenes.Sum(o => o.Horas!.Value)
            : null;
        fila.HorasDestino = e.HorasDestino;

        fila.PorcentajeContenidos = e.Contenidos;
        fila.PorcentajeProfundidad = e.Profundidad;
        fila.PorcentajeCargaHoraria = e.CargaHoraria;
        fila.MinimoExigido = Math.Round((decimal)(resultado.Minimo ?? 80d), 2);
        fila.SinSilabo = e.SinSilabo;
        fila.EstimacionAgente = Recortar(e.Estimacion, 30);

        fila.Aprobaciones = final?.Aprobaciones ?? 0;
        fila.Rechazos = final?.Rechazos ?? 0;
        fila.ResultadoFinal = e.BloqueadaPorCreditos ? "NO_HOMOLOGADA" : final?.Resultado ?? "PENDIENTE";

        if (c.AnalizadoUtc is { } analizado) fila.AnalizadoUtc = analizado;
        fila.ActualizadoUtc = ahora;
    }

    private static string Recortar(string? texto, int maximo)
    {
        texto ??= string.Empty;
        return texto.Length <= maximo ? texto : texto[..maximo];
    }
}
