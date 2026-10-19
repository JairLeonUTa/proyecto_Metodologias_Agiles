using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Siha.Contracts;
using Siha.MsB.Api.Contracts.Dtos;
using Siha.MsB.Api.Data;
using Siha.MsB.Api.Domain.Entities;
using Siha.MsB.Api.Services;

namespace Siha.MsB.Api.Controllers;

/// <summary>
/// Registro, envío y consulta de expedientes (US-05 / US-06).
///
/// Flujo (Sprint 1, revisión de integración de documentos):
///  1. <c>POST /api/expedientes</c> crea el expediente en <c>PENDIENTE_DOCUMENTOS</c>; NO publica.
///  2. <c>POST /api/expedientes/{id}/documentos</c> carga y valida cada PDF en el servidor.
///  3. <c>POST /api/expedientes/{id}/enviar</c> verifica en disco los documentos aceptados y, si se
///     cumplen los requisitos documentales, publica <c>SolicitudCreadaEvent</c>.
///  4. <c>POST /api/expedientes/{id}/republicar</c> reintenta tras un fallo del bus (también lo hace
///     automáticamente <see cref="RepublicacionAutomaticaWorker"/>), conservando el CorrelationId.
/// </summary>
[ApiController]
[Route("api/expedientes")]
[Authorize(Roles = $"{RolesSistema.Operador},{RolesSistema.Administrador}")]
public class ExpedientesController : ControllerBase
{
    private readonly SihaDbContextB _db;
    private readonly ExpedienteEnvioService _envio;
    private readonly ILogger<ExpedientesController> _logger;

    public ExpedientesController(
        SihaDbContextB db,
        ExpedienteEnvioService envio,
        ILogger<ExpedientesController> logger)
    {
        _db = db;
        _envio = envio;
        _logger = logger;
    }

    /// <summary>
    /// Registra el expediente como borrador (<c>PENDIENTE_DOCUMENTOS</c>) y devuelve 201 Created.
    /// Los documentos NO se aceptan aquí: deben cargarse como PDF en
    /// <c>POST /api/expedientes/{id}/documentos</c>, donde el servidor calcula ruta, hash y tamaño.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ExpedienteAceptadoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ExpedienteAceptadoResponse>> Registrar(
        [FromBody] CrearExpedienteRequest request,
        CancellationToken cancellationToken)
    {
        if (!TiposTramite.Todos.Contains(request.TipoTramite))
        {
            return BadRequest(new { mensaje = $"Tipo de trámite inválido. Valores permitidos: {string.Join(", ", TiposTramite.Todos)}." });
        }

        if (!RutasNormativas.Todas.Contains(request.RutaNormativa))
        {
            return BadRequest(new { mensaje = $"Ruta normativa inválida. Valores permitidos: {string.Join(", ", RutasNormativas.Todas)}." });
        }

        if (request.Documentos is { Count: > 0 })
        {
            // No se confía en rutas, hashes ni tamaños declarados por el cliente.
            return BadRequest(new
            {
                mensaje = "Los documentos no se declaran en el registro. Cargue cada PDF en " +
                          "POST /api/expedientes/{id}/documentos y luego envíe el expediente con POST /api/expedientes/{id}/enviar."
            });
        }

        var expediente = ConstruirExpediente(request);
        _db.Expedientes.Add(expediente);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Expediente registrado como borrador. ExpedienteId={ExpedienteId} Codigo={Codigo} CorrelationId={CorrelationId}",
            expediente.Id, expediente.Codigo, expediente.CorrelationId);

        var respuesta = new ExpedienteAceptadoResponse(
            expediente.Id,
            expediente.Codigo,
            expediente.Estado,
            expediente.CorrelationId,
            false,
            "Expediente registrado. Cargue los documentos PDF y luego envíelo para su procesamiento.");

        return CreatedAtAction(nameof(Obtener), new { id = expediente.Id }, respuesta);
    }

    /// <summary>
    /// Envía el expediente: verifica en disco los PDF aceptados y publica el evento solo si se
    /// cumplen los requisitos documentales. Idempotente: un expediente publicado no se republica.
    /// </summary>
    [HttpPost("{id:guid}/enviar")]
    [ProducesResponseType(typeof(ExpedienteAceptadoResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ExpedienteAceptadoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ExpedienteAceptadoResponse>> Enviar(Guid id, CancellationToken cancellationToken)
        => ARespuesta(await _envio.EnviarAsync(id, ModoEnvio.Envio, cancellationToken));

    /// <summary>
    /// Reintenta la publicación de un expediente en estado recuperable, conservando el
    /// identificador de correlación original (CA3). Vuelve a verificar los documentos en disco.
    /// </summary>
    [HttpPost("{id:guid}/republicar")]
    [ProducesResponseType(typeof(ExpedienteAceptadoResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ExpedienteAceptadoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ExpedienteAceptadoResponse>> Republicar(Guid id, CancellationToken cancellationToken)
        => ARespuesta(await _envio.EnviarAsync(id, ModoEnvio.Reintento, cancellationToken));

    private ActionResult<ExpedienteAceptadoResponse> ARespuesta(ResultadoEnvio resultado)
    {
        if (resultado.Expediente is null)
        {
            return NotFound(new { mensaje = resultado.Mensaje });
        }

        var expediente = resultado.Expediente;
        var cuerpo = new ExpedienteAceptadoResponse(
            expediente.Id,
            expediente.Codigo,
            expediente.Estado,
            expediente.CorrelationId,
            expediente.EventoPublicado,
            resultado.Mensaje);

        return resultado.Tipo switch
        {
            TipoResultadoEnvio.Publicado => Accepted(cuerpo),
            TipoResultadoEnvio.FalloPublicacion => Accepted(cuerpo),
            TipoResultadoEnvio.YaPublicado => Ok(cuerpo),
            TipoResultadoEnvio.RequisitosIncumplidos => UnprocessableEntity(new
            {
                mensaje = resultado.Mensaje,
                detalles = resultado.Detalles,
                expedienteId = expediente.Id,
                estado = expediente.Estado
            }),
            TipoResultadoEnvio.NoEncontrado => NotFound(new { mensaje = resultado.Mensaje }),
            _ => Conflict(new { mensaje = resultado.Mensaje, expedienteId = expediente.Id, estado = expediente.Estado })
        };
    }

    /// <summary>Consulta el estado de recepción y procesamiento del expediente (US-05, CA3).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ExpedienteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpedienteResponse>> Obtener(Guid id, CancellationToken cancellationToken)
    {
        var expediente = await _db.Expedientes
            .Include(e => e.Documentos)
            .Include(e => e.Asignaturas)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        return expediente is null ? NotFound() : Ok(AExpedienteResponse(expediente));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ExpedienteResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ExpedienteResponse>>> Listar(
        [FromQuery] string? estado,
        CancellationToken cancellationToken)
    {
        var query = _db.Expedientes
            .Include(e => e.Documentos)
            .Include(e => e.Asignaturas)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(estado))
        {
            query = query.Where(e => e.Estado == estado);
        }

        var expedientes = await query.OrderByDescending(e => e.FechaCreacionUtc).Take(100).ToListAsync(cancellationToken);
        return Ok(expedientes.Select(AExpedienteResponse));
    }

    private static Expediente ConstruirExpediente(CrearExpedienteRequest request)
    {
        var expediente = new Expediente
        {
            Codigo = $"EXP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            TipoTramite = request.TipoTramite,
            RutaNormativa = request.RutaNormativa,
            CarreraOrigenCodigo = request.CarreraOrigenCodigo.Trim(),
            CarreraDestinoCodigo = request.CarreraDestinoCodigo.Trim(),
            InstitucionOrigen = request.InstitucionOrigen?.Trim() ?? string.Empty,
            Estado = ExpedienteEstados.PendienteDocumentos,
            CorrelationId = Guid.NewGuid()
        };

        foreach (var asignatura in request.Asignaturas ?? Array.Empty<AsignaturaRequest>())
        {
            expediente.Asignaturas.Add(new ExpedienteAsignatura
            {
                AsignaturaOrigenCodigo = asignatura.CodigoOrigen,
                AsignaturaOrigenNombre = asignatura.NombreOrigen,
                AsignaturaDestinoCodigo = asignatura.CodigoDestino,
                NotaOrigen = asignatura.Nota,
                // Fecha en la que la institución de origen certificó la aprobación de la materia
                // seleccionada del catálogo (requisito RN-03 de CAU-P-122).
                FechaAprobacionCertificada = asignatura.FechaAprobacionCertificada
            });
        }

        return expediente;
    }

    private static ExpedienteResponse AExpedienteResponse(Expediente expediente) => new(
        expediente.Id,
        expediente.Codigo,
        expediente.TipoTramite,
        expediente.RutaNormativa,
        expediente.CarreraOrigenCodigo,
        expediente.CarreraDestinoCodigo,
        expediente.Estado,
        expediente.CorrelationId,
        expediente.EventoPublicado,
        expediente.IntentosPublicacion,
        expediente.UltimoErrorPublicacion,
        expediente.FechaCreacionUtc,
        expediente.FechaPublicacionUtc,
        expediente.Documentos
            .Select(MapeosExpediente.ADto)
            .ToList(),
        expediente.Asignaturas
            .Select(a => new AsignaturaResponse(
                a.AsignaturaOrigenCodigo,
                a.AsignaturaOrigenNombre,
                a.AsignaturaDestinoCodigo,
                a.NotaOrigen,
                a.FechaAprobacionCertificada))
            .ToList());
}
