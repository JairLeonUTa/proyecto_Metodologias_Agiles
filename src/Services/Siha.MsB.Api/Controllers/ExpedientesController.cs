using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Siha.Contracts;
using Siha.Contracts.Events;
using Siha.MsB.Api.Contracts.Dtos;
using Siha.MsB.Api.Data;
using Siha.MsB.Api.Domain.Entities;
using Siha.MsB.Api.Events;

namespace Siha.MsB.Api.Controllers;

/// <summary>
/// Registro y consulta de expedientes (US-05) con publicación asíncrona del evento (US-06,
/// tareas S1-J07 a S1-J09). El recorrido persiste primero en DB B y luego publica en RabbitMQ;
/// si la publicación falla, el expediente queda en un estado recuperable (CA3).
/// </summary>
[ApiController]
[Route("api/expedientes")]
[Authorize(Roles = $"{RolesSistema.Operador},{RolesSistema.Administrador}")]
public class ExpedientesController : ControllerBase
{
    private readonly SihaDbContextB _db;
    private readonly IExpedienteEventPublisher _publisher;
    private readonly ILogger<ExpedientesController> _logger;

    public ExpedientesController(
        SihaDbContextB db,
        IExpedienteEventPublisher publisher,
        ILogger<ExpedientesController> logger)
    {
        _db = db;
        _publisher = publisher;
        _logger = logger;
    }

    /// <summary>
    /// Registra un expediente válido y publica <c>SolicitudCreadaEvent</c> en el bus.
    /// Devuelve HTTP 202 Accepted con el estado PENDIENTE_CONTENIDOS (CA2).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ExpedienteAceptadoResponse), StatusCodes.Status202Accepted)]
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

        var expediente = ConstruirExpediente(request);

        // 1) Persistencia primero: el expediente existe aunque falle el bus (CA3).
        _db.Expedientes.Add(expediente);
        await _db.SaveChangesAsync(cancellationToken);

        // 2) Publicación en el bus con reintentos.
        var (publicado, error) = await _publisher.PublicarAsync(ConstruirEvento(expediente), cancellationToken);

        expediente.IntentosPublicacion += 1;

        if (publicado)
        {
            expediente.EventoPublicado = true;
            expediente.Estado = ExpedienteEstados.PendienteContenidos;
            expediente.FechaPublicacionUtc = DateTimeOffset.UtcNow;
            expediente.UltimoErrorPublicacion = null;
        }
        else
        {
            expediente.Estado = ExpedienteEstados.FalloPublicacion;
            expediente.UltimoErrorPublicacion = error;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var respuesta = new ExpedienteAceptadoResponse(
            expediente.Id,
            expediente.Codigo,
            expediente.Estado,
            expediente.CorrelationId,
            publicado,
            publicado
                ? "Solicitud aceptada y publicada en el bus para procesamiento asíncrono."
                : "Solicitud persistida, pero la publicación en el bus falló; el expediente quedó en estado recuperable.");

        return Accepted(respuesta);
    }

    /// <summary>
    /// Reintenta la publicación conservando el identificador de correlación original (CA3).
    /// </summary>
    [HttpPost("{id:guid}/republicar")]
    [ProducesResponseType(typeof(ExpedienteAceptadoResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpedienteAceptadoResponse>> Republicar(Guid id, CancellationToken cancellationToken)
    {
        var expediente = await _db.Expedientes
            .Include(e => e.Documentos)
            .Include(e => e.Asignaturas)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (expediente is null)
        {
            return NotFound();
        }

        var (publicado, error) = await _publisher.PublicarAsync(ConstruirEvento(expediente), cancellationToken);

        expediente.IntentosPublicacion += 1;

        if (publicado)
        {
            expediente.EventoPublicado = true;
            expediente.Estado = ExpedienteEstados.PendienteContenidos;
            expediente.FechaPublicacionUtc = DateTimeOffset.UtcNow;
            expediente.UltimoErrorPublicacion = null;
        }
        else
        {
            expediente.Estado = ExpedienteEstados.FalloPublicacion;
            expediente.UltimoErrorPublicacion = error;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Accepted(new ExpedienteAceptadoResponse(
            expediente.Id,
            expediente.Codigo,
            expediente.Estado,
            expediente.CorrelationId,
            publicado,
            publicado ? "Republicación exitosa." : "La republicación volvió a fallar; se conserva el CorrelationId para un nuevo intento."));
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
            Estado = ExpedienteEstados.Registrado,
            CorrelationId = Guid.NewGuid()
        };

        foreach (var documento in request.Documentos ?? Array.Empty<DocumentoRequest>())
        {
            expediente.Documentos.Add(new ExpedienteDocumento
            {
                NombreArchivo = documento.NombreArchivo,
                RutaAlmacenamiento = documento.RutaAlmacenamiento,
                HashSha256 = documento.HashSha256 ?? string.Empty,
                TamanoBytes = documento.TamanoBytes
            });
        }

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

    private static SolicitudCreadaEvent ConstruirEvento(Expediente expediente) => new()
    {
        ExpedienteId = expediente.Id,
        CorrelationId = expediente.CorrelationId,
        CodigoExpediente = expediente.Codigo,
        TipoTramite = expediente.TipoTramite,
        RutaNormativa = expediente.RutaNormativa,
        CarreraOrigenCodigo = expediente.CarreraOrigenCodigo,
        CarreraDestinoCodigo = expediente.CarreraDestinoCodigo,
        DocumentoIds = expediente.Documentos.Select(d => d.Id).ToList(),
        AsignaturasOrigen = expediente.Asignaturas.Select(a => a.AsignaturaOrigenCodigo).ToList(),
        Estado = ExpedienteEstados.PendienteContenidos,
        FechaCreacionUtc = expediente.FechaCreacionUtc
    };

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
