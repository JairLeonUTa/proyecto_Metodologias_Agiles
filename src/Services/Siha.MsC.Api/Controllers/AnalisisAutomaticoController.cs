using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Siha.Contracts;
using Siha.MsC.Api.Contracts.Dtos;
using Siha.MsC.Api.Services;

namespace Siha.MsC.Api.Controllers;

/// <summary>
/// Solicitudes con certificado SIHA: estimación del agente y respuesta final de los dos docentes.
/// Ruta bajo <c>/api/extracciones/...</c> para que el Gateway la encamine a MS C.
/// </summary>
[ApiController]
[Route("api/extracciones/expedientes")]
public class AnalisisAutomaticoController : ControllerBase
{
    private readonly IAnalisisAutomaticoService _agente;
    private readonly SolicitudesAnalisisService _solicitudes;

    public AnalisisAutomaticoController(IAnalisisAutomaticoService agente, SolicitudesAnalisisService solicitudes)
    {
        _agente = agente;
        _solicitudes = solicitudes;
    }

    /// <summary>Estimación del agente, revisores, respuestas de los docentes y resultado final.</summary>
    [HttpGet("{expedienteId:guid}/analisis")]
    [Authorize(Roles = $"{RolesSistema.Operador},{RolesSistema.Administrador},{RolesSistema.Docente}")]
    [ProducesResponseType(typeof(SolicitudAnalisisResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SolicitudAnalisisResponse>> Obtener(Guid expedienteId, CancellationToken cancellationToken)
    {
        var solicitud = await _solicitudes.ObtenerAsync(expedienteId, Correo(), EsDocente(), cancellationToken);
        return solicitud is null
            ? NotFound(new { mensaje = "La solicitud no existe, todavía no se procesa o no está asignada a este docente." })
            : Ok(solicitud);
    }

    /// <summary>Vuelve a ejecutar el análisis (por ejemplo, si la IA no estaba disponible).</summary>
    [HttpPost("{expedienteId:guid}/analisis-automatico")]
    [Authorize(Roles = $"{RolesSistema.Operador},{RolesSistema.Administrador},{RolesSistema.Docente}")]
    [ProducesResponseType(typeof(SolicitudAnalisisResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SolicitudAnalisisResponse>> Reanalizar(Guid expedienteId, CancellationToken cancellationToken)
    {
        if (await _solicitudes.ObtenerAsync(expedienteId, Correo(), EsDocente(), cancellationToken) is null)
        {
            return NotFound(new { mensaje = "La solicitud no existe o no está asignada a este docente." });
        }

        await _agente.AnalizarYGuardarAsync(expedienteId, cancellationToken);
        return Ok(await _solicitudes.ObtenerAsync(expedienteId, Correo(), EsDocente(), cancellationToken));
    }

    /// <summary>Respuesta final del docente revisor que inicia sesión (de acuerdo o no, materia por materia).</summary>
    [HttpPost("{expedienteId:guid}/dictamen")]
    [Authorize(Roles = RolesSistema.Docente)]
    [ProducesResponseType(typeof(SolicitudAnalisisResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SolicitudAnalisisResponse>> Responder(
        Guid expedienteId, [FromBody] DictamenRequest dictamen, CancellationToken cancellationToken)
    {
        var correo = Correo();
        if (string.IsNullOrWhiteSpace(correo))
        {
            return Forbid();
        }

        var (respuesta, error, codigo) = await _solicitudes.ResponderAsync(
            expedienteId, correo, User.FindFirst(ClaimTypes.Name)?.Value ?? correo, dictamen, cancellationToken);
        return respuesta is null ? StatusCode(codigo, new { mensaje = error }) : Ok(respuesta);
    }

    private string? Correo() => User.FindFirst(ClaimTypes.Email)?.Value;

    private bool EsDocente() => User.IsInRole(RolesSistema.Docente);
}
