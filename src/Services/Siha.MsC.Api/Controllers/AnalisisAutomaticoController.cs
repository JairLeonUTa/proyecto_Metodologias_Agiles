using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Siha.Contracts;
using Siha.MsC.Api.Contracts.Dtos;
using Siha.MsC.Api.Services;

namespace Siha.MsC.Api.Controllers;

/// <summary>
/// Análisis automático por contenidos de un expediente completo (certificado SIHA).
/// Ruta bajo <c>/api/extracciones/...</c> para que el Gateway la encamine a MS C.
/// </summary>
[ApiController]
[Route("api/extracciones/expedientes")]
public class AnalisisAutomaticoController : ControllerBase
{
    private readonly IAnalisisAutomaticoService _servicio;

    public AnalisisAutomaticoController(IAnalisisAutomaticoService servicio) => _servicio = servicio;

    /// <summary>
    /// Compara las materias aprobadas del expediente con todas las materias de la carrera de destino y
    /// devuelve las homologaciones propuestas, las que no alcanzan el contenido y las materias sin uso.
    /// </summary>
    [HttpPost("{expedienteId:guid}/analisis-automatico")]
    [Authorize(Roles = $"{RolesSistema.Operador},{RolesSistema.Administrador},{RolesSistema.Docente}")]
    [ProducesResponseType(typeof(AnalisisAutomaticoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AnalisisAutomaticoResponse>> Analizar(
        Guid expedienteId,
        [FromBody] AnalisisAutomaticoRequest solicitud,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _servicio.AnalizarAsync(expedienteId, solicitud, cancellationToken));
        }
        catch (AnalisisAutomaticoException ex)
        {
            return StatusCode(ex.CodigoHttp, new { mensaje = ex.Message, detalles = ex.Detalles });
        }
    }
}
