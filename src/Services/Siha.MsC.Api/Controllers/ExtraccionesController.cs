using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Siha.Contracts;
using Siha.MsC.Api.Contracts.Dtos;
using Siha.MsC.Api.Domain;
using Siha.MsC.Api.Domain.Entities;
using Siha.MsC.Api.Services;

namespace Siha.MsC.Api.Controllers;

/// <summary>
/// Consulta y confirmación humana de los datos extraídos de un PDF (US-07, S2-J02).
/// El operador revisa créditos, horas y unidades; la evaluación no avanza hasta confirmarlos.
/// </summary>
[ApiController]
[Route("api/extracciones")]
public class ExtraccionesController : ControllerBase
{
    private readonly ExtraccionService _servicio;

    public ExtraccionesController(ExtraccionService servicio)
    {
        _servicio = servicio;
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = $"{RolesSistema.Operador},{RolesSistema.Administrador},{RolesSistema.Docente}")]
    [ProducesResponseType(typeof(ExtraccionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExtraccionResponse>> Obtener(Guid id, CancellationToken cancellationToken)
    {
        var extraccion = await _servicio.ObtenerAsync(id, cancellationToken);

        return extraccion is null
            ? NotFound(new { mensaje = $"La extracción {id} no existe." })
            : Ok(ARespuesta(extraccion));
    }

    /// <summary>
    /// Confirma o corrige la extracción. Solo el OPERADOR (o ADMINISTRADOR) puede validar los
    /// datos; una extracción ya confirmada o pendiente de OCR/LLM devuelve 409.
    /// </summary>
    [HttpPut("{id:guid}/confirmar")]
    [Authorize(Roles = $"{RolesSistema.Operador},{RolesSistema.Administrador}")]
    [ProducesResponseType(typeof(ExtraccionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ExtraccionResponse>> Confirmar(
        Guid id,
        [FromBody] ConfirmarExtraccionRequest? request,
        CancellationToken cancellationToken)
    {
        var usuario = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(usuario))
        {
            usuario = User.FindFirstValue(ClaimTypes.Email)
                ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? "desconocido";
        }

        var resultado = await _servicio.ConfirmarAsync(id, request, usuario, cancellationToken);

        return resultado.Resultado switch
        {
            ResultadoConfirmacionExtraccion.Confirmada => Ok(ARespuesta(resultado.Extraccion!)),
            ResultadoConfirmacionExtraccion.NoEncontrada =>
                NotFound(new { mensaje = $"La extracción {id} no existe." }),
            ResultadoConfirmacionExtraccion.DatosInvalidos => BadRequest(new
            {
                mensaje = "Los datos de confirmación no son válidos.",
                errores = resultado.Errores
            }),
            ResultadoConfirmacionExtraccion.YaConfirmada => Conflict(new
            {
                mensaje = "La extracción ya fue confirmada.",
                estado = resultado.Extraccion!.Estado,
                confirmadoPor = resultado.Extraccion.ConfirmadoPor
            }),
            ResultadoConfirmacionExtraccion.PendienteOcrLlm => Conflict(new
            {
                mensaje = "La extracción está pendiente de OCR/LLM; complete el flujo US-10 antes de confirmarla.",
                estado = resultado.Extraccion!.Estado
            }),
            _ => Conflict(new
            {
                mensaje = "La extracción no está en estado PENDIENTE_CONFIRMACION.",
                estado = resultado.Extraccion!.Estado
            })
        };
    }

    private static ExtraccionResponse ARespuesta(Extraccion extraccion) => new(
        extraccion.Id,
        extraccion.ExpedienteId,
        extraccion.DocumentoId,
        extraccion.CodigoAsignaturaOrigen,
        extraccion.NombreAsignaturaOrigen,
        extraccion.Creditos,
        extraccion.Horas,
        extraccion.Confianza,
        extraccion.Estado,
        extraccion.Estado == EstadosExtraccion.PendienteConfirmacion,
        extraccion.Unidades
            .OrderBy(u => u.Numero)
            .Select(u => new UnidadExtraccionResponse(u.Numero, u.Nombre, u.Horas, u.Contenidos))
            .ToList(),
        extraccion.CreadoUtc,
        extraccion.ConfirmadoUtc,
        extraccion.ConfirmadoPor,
        extraccion.Observacion);
}
