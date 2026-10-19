using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Siha.Contracts;
using Siha.MsA.Api.Contracts.Dtos;
using Siha.MsA.Api.Services;

namespace Siha.MsA.Api.Controllers;

/// <summary>
/// Reglas y umbrales de evaluación (US-02, tarea S1-J03).
/// La consulta está disponible para usuarios autenticados; la modificación exige rol
/// Administrador (Docente u Operador reciben 403, CA3).
/// </summary>
[ApiController]
[Route("api/reglas")]
[Authorize]
public class ReglasController : ControllerBase
{
    private readonly IReglasService _reglas;

    public ReglasController(IReglasService reglas)
    {
        _reglas = reglas;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ReglaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ReglaDto>>> Listar(
        [FromQuery] string? rutaNormativa,
        [FromQuery] string? tipoTramite)
    {
        var reglas = await _reglas.ListarReglasAsync(rutaNormativa, tipoTramite);
        return Ok(reglas);
    }

    [HttpPost]
    [Authorize(Roles = RolesSistema.Administrador)]
    [ProducesResponseType(typeof(ReglaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ReglaDto>> Crear([FromBody] CrearReglaRequest request)
    {
        var (ok, error, regla) = await _reglas.CrearReglaAsync(request);
        if (!ok)
        {
            return BadRequest(new { mensaje = error });
        }

        return CreatedAtAction(nameof(Listar), new { rutaNormativa = regla!.RutaNormativa }, regla);
    }

    /// <summary>Consulta los umbrales heurísticos θ, δ, τ vigentes de una ruta normativa.</summary>
    [HttpGet("parametros/{rutaNormativa}")]
    [ProducesResponseType(typeof(ParametroEvaluacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ParametroEvaluacionDto>> ObtenerParametros(string rutaNormativa)
    {
        var parametros = await _reglas.ObtenerParametrosAsync(rutaNormativa);
        return parametros is null
            ? NotFound(new { mensaje = $"No hay parámetros configurados para la ruta {rutaNormativa}." })
            : Ok(parametros);
    }

    /// <summary>
    /// Actualiza los umbrales θ, δ, τ. Valores fuera de rango o una banda inválida (δ > θ)
    /// producen 400 Bad Request sin modificar la configuración anterior (CA2).
    /// </summary>
    [HttpPut("parametros/{rutaNormativa}")]
    [Authorize(Roles = RolesSistema.Administrador)]
    [ProducesResponseType(typeof(ParametroEvaluacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ParametroEvaluacionDto>> ActualizarParametros(
        string rutaNormativa,
        [FromBody] ActualizarParametrosRequest request)
    {
        var usuario = User.Identity?.Name ?? "sistema";
        var (ok, error, parametros) = await _reglas.ActualizarParametrosAsync(rutaNormativa, request, usuario);

        if (!ok)
        {
            return BadRequest(new { mensaje = error });
        }

        return Ok(parametros);
    }
}
