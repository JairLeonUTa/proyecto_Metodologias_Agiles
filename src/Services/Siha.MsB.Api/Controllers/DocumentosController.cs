using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Siha.Contracts;
using Siha.MsB.Api.Contracts.Dtos;
using Siha.MsB.Api.Data;
using Siha.MsB.Api.Domain;
using Siha.MsB.Api.Domain.Entities;
using Siha.MsB.Api.Services;

namespace Siha.MsB.Api.Controllers;

/// <summary>
/// Carga y consulta de los documentos PDF de un expediente (Sprint 1 - S1-S05 / S1-S06).
///
/// Reglas aplicadas:
///  - Solo se aceptan PDF reales (firma binaria <c>%PDF</c>), con extensión y tipo MIME correctos
///    y tamaño dentro del máximo configurable (<c>Archivos:TamanoMaximoBytes</c>).
///  - Cualquier rechazo devuelve <b>400 Bad Request</b> y queda auditado en EXPEDIENTE_DOC con el
///    estado <c>RECHAZADO</c>, visible en <c>GET /api/expedientes/{id}/documentos</c>.
///  - El archivo se guarda con un nombre generado (GUID) bajo <c>Archivos:RutaAlmacenamiento</c> y
///    se persiste su hash SHA-256 junto con los metadatos.
/// </summary>
[ApiController]
[Route("api/expedientes")]
[Authorize(Roles = $"{RolesSistema.Operador},{RolesSistema.Administrador}")]
public class DocumentosController : ControllerBase
{
    private readonly SihaDbContextB _db;
    private readonly IArchivoStorageService _archivos;
    private readonly ILogger<DocumentosController> _logger;

    public DocumentosController(
        SihaDbContextB db,
        IArchivoStorageService archivos,
        ILogger<DocumentosController> logger)
    {
        _db = db;
        _archivos = archivos;
        _logger = logger;
    }

    /// <summary>
    /// Carga un documento PDF al expediente indicado (multipart/form-data, campo <c>archivo</c>).
    /// Devuelve 201 Created con los metadatos persistidos en EXPEDIENTE_DOC.
    /// </summary>
    [HttpPost("{id:guid}/documentos")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(DocumentoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentoResponse>> Subir(
        Guid id,
        [FromForm] IFormFile? archivo,
        CancellationToken cancellationToken)
    {
        var expediente = await _db.Expedientes
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (expediente is null)
        {
            return NotFound(new { mensaje = $"No existe el expediente '{id}'." });
        }

        var validacion = _archivos.ValidarPdf(archivo);

        if (!validacion.Valido)
        {
            // Se audita el intento rechazado (solo si llegó un archivo) para que el operador
            // pueda consultar por qué no se aceptó la carga.
            if (archivo is not null)
            {
                _db.ExpedienteDocumentos.Add(new ExpedienteDocumento
                {
                    ExpedienteId = expediente.Id,
                    NombreArchivo = archivo.FileName ?? string.Empty,
                    NombreAlmacenado = string.Empty,
                    RutaAlmacenamiento = string.Empty,
                    HashSha256 = string.Empty,
                    TamanoBytes = archivo.Length,
                    MimeType = archivo.ContentType ?? string.Empty,
                    EstadoRecepcion = EstadosRecepcionDocumento.Rechazado,
                    ObservacionValidacion = validacion.Error
                });

                await _db.SaveChangesAsync(cancellationToken);
            }

            _logger.LogWarning(
                "Documento rechazado. ExpedienteId={ExpedienteId} Nombre={Nombre} Motivo={Motivo}",
                id, archivo?.FileName, validacion.Error);

            return BadRequest(new
            {
                mensaje = validacion.Error,
                expedienteId = id,
                tamanoMaximoBytes = _archivos.TamanoMaximoBytes
            });
        }

        var almacenado = await _archivos.GuardarAsync(archivo!, cancellationToken);

        var documento = new ExpedienteDocumento
        {
            ExpedienteId = expediente.Id,
            NombreArchivo = almacenado.NombreOriginal,
            NombreAlmacenado = almacenado.NombreAlmacenado,
            RutaAlmacenamiento = almacenado.RutaAlmacenamiento,
            HashSha256 = almacenado.HashSha256,
            TamanoBytes = almacenado.TamanoBytes,
            MimeType = almacenado.MimeType,
            EstadoRecepcion = EstadosRecepcionDocumento.Validado,
            FechaCargaUtc = DateTimeOffset.UtcNow
        };

        _db.ExpedienteDocumentos.Add(documento);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Documento cargado. ExpedienteId={ExpedienteId} DocumentoId={DocumentoId} Hash={Hash}",
            expediente.Id, documento.Id, documento.HashSha256);

        return CreatedAtAction(
            nameof(Listar),
            new { id = expediente.Id },
            MapeosExpediente.ADto(documento));
    }

    /// <summary>
    /// Devuelve los metadatos de los documentos del expediente junto con su estado de recepción
    /// (VALIDADO / RECHAZADO). Permite filtrar por <paramref name="estado"/>.
    /// </summary>
    [HttpGet("{id:guid}/documentos")]
    [ProducesResponseType(typeof(DocumentosExpedienteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentosExpedienteResponse>> Listar(
        Guid id,
        [FromQuery] string? estado,
        CancellationToken cancellationToken)
    {
        var expediente = await _db.Expedientes
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (expediente is null)
        {
            return NotFound(new { mensaje = $"No existe el expediente '{id}'." });
        }

        var query = _db.ExpedienteDocumentos
            .AsNoTracking()
            .Where(d => d.ExpedienteId == id);

        if (!string.IsNullOrWhiteSpace(estado))
        {
            query = query.Where(d => d.EstadoRecepcion == estado);
        }

        var documentos = await query
            .OrderByDescending(d => d.FechaCargaUtc)
            .ToListAsync(cancellationToken);

        return Ok(MapeosExpediente.ADto(expediente, documentos.Select(MapeosExpediente.ADto).ToList()));
    }
}
