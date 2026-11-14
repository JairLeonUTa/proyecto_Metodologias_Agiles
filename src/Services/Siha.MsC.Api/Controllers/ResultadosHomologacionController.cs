using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Siha.Contracts;
using Siha.MsC.Api.Data;
using Siha.MsC.Api.Domain.Entities;

namespace Siha.MsC.Api.Controllers;

/// <summary>
/// Consulta de la tabla RESULTADO_HOMOLOGACION: una fila por materia pedida con los porcentajes del
/// agente y la decisión de los docentes. Ruta bajo <c>/api/extracciones/...</c> para que el Gateway la
/// encamine a MS C.
/// </summary>
[ApiController]
[Route("api/extracciones/resultados-homologacion")]
[Authorize(Roles = $"{RolesSistema.Operador},{RolesSistema.Administrador}")]
public class ResultadosHomologacionController : ControllerBase
{
    private readonly SihaDbContextC _db;

    public ResultadosHomologacionController(SihaDbContextC db) => _db = db;

    /// <summary>
    /// Lista los resultados, del más reciente al más antiguo. Filtros opcionales: tipo de trámite
    /// (INTERNO / EXTERNO), resultado final (PENDIENTE, HOMOLOGADA, NO_HOMOLOGADA, SIN_ACUERDO) y expediente.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ResultadoHomologacion>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ResultadoHomologacion>>> Listar(
        [FromQuery] string? tipoTramite,
        [FromQuery] string? resultado,
        [FromQuery] Guid? expedienteId,
        CancellationToken cancellationToken)
    {
        var consulta = _db.ResultadosHomologacion.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(tipoTramite))
            consulta = consulta.Where(r => r.TipoTramite == tipoTramite.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(resultado))
            consulta = consulta.Where(r => r.ResultadoFinal == resultado.Trim().ToUpperInvariant());
        if (expedienteId is { } id)
            consulta = consulta.Where(r => r.ExpedienteId == id);

        var filas = await consulta
            .OrderByDescending(r => r.AnalizadoUtc)
            .ThenBy(r => r.NumeroSolicitud)
            .Take(500)
            .ToListAsync(cancellationToken);
        return Ok(filas);
    }
}
