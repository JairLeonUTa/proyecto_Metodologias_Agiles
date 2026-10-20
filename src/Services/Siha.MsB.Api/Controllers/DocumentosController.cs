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
///  - Solo se admiten cargas mientras el expediente está en <c>PENDIENTE_DOCUMENTOS</c> (409 en otro caso).
///  - Reintentos sin duplicados: si el mismo contenido (SHA-256 calculado en el servidor) ya está
///    VALIDADO en el expediente, se devuelve 200 con el documento existente y no se guarda otra copia.
///  - Fallo del almacenamiento físico: 503 sin filas ni archivos huérfanos; el cliente puede reintentar.
///  - Si falla la base de datos después de escribir el archivo, el archivo se elimina (compensación).
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

        if (expediente.Estado != ExpedienteEstados.PendienteDocumentos)
        {
            return Conflict(new
            {
                mensaje = $"El expediente está en estado '{expediente.Estado}'; solo se admiten documentos mientras está en {ExpedienteEstados.PendienteDocumentos}.",
                expedienteId = id,
                estado = expediente.Estado
            });
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
                    NombreArchivo = Recortar(archivo.FileName, 255),
                    NombreAlmacenado = string.Empty,
                    RutaAlmacenamiento = string.Empty,
                    HashSha256 = string.Empty,
                    TamanoBytes = archivo.Length,
                    MimeType = Recortar(archivo.ContentType, 100),
                    EstadoRecepcion = EstadosRecepcionDocumento.Rechazado,
                    ObservacionValidacion = Recortar(validacion.Error, 500)
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

        // Reintento idempotente: el hash lo calcula el servidor a partir del contenido recibido.
        var hash = await _archivos.CalcularHashAsync(archivo!, cancellationToken);
        var existente = await _db.ExpedienteDocumentos
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.ExpedienteId == id &&
                                      d.HashSha256 == hash &&
                                      d.EstadoRecepcion == EstadosRecepcionDocumento.Validado,
                cancellationToken);

        if (existente is not null)
        {
            _logger.LogInformation(
                "Carga repetida ignorada (mismo SHA-256). ExpedienteId={ExpedienteId} DocumentoId={DocumentoId}",
                id, existente.Id);
            return Ok(MapeosExpediente.ADto(existente));
        }

        DocumentoAlmacenado almacenado;
        try
        {
            almacenado = await _archivos.GuardarAsync(archivo!, cancellationToken);
        }
        catch (AlmacenamientoNoDisponibleException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { mensaje = ex.Message, expedienteId = id });
        }

        // Bucle de guardado: renovar el sello del expediente impide que una carga se cuele mientras otra
        // petición lo está enviando, pero también hace chocar entre sí a cargas legítimas simultáneas.
        // Ante un choque se recarga el expediente y: si ya no admite cargas -> 409; si otra carga
        // simultánea guardó el mismo contenido -> 200 con ese documento; si no -> se reintenta.
        for (var intento = 1; ; intento++)
        {
            var documento = new ExpedienteDocumento
            {
                ExpedienteId = expediente.Id,
                NombreArchivo = Recortar(almacenado.NombreOriginal, 255),
                NombreAlmacenado = almacenado.NombreAlmacenado,
                RutaAlmacenamiento = almacenado.RutaAlmacenamiento,
                HashSha256 = almacenado.HashSha256,
                TamanoBytes = almacenado.TamanoBytes,
                MimeType = almacenado.MimeType,
                EstadoRecepcion = EstadosRecepcionDocumento.Validado,
                FechaCargaUtc = DateTimeOffset.UtcNow
            };

            _db.ExpedienteDocumentos.Add(documento);
            expediente.SelloConcurrencia = Guid.NewGuid();

            try
            {
                await _db.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Documento cargado. ExpedienteId={ExpedienteId} DocumentoId={DocumentoId} Hash={Hash} Intento={Intento}",
                    expediente.Id, documento.Id, documento.HashSha256, intento);

                return CreatedAtAction(
                    nameof(Listar),
                    new { id = expediente.Id },
                    MapeosExpediente.ADto(documento));
            }
            catch (Exception ex) when (ex is DbUpdateConcurrencyException || (ex is DbUpdateException dbEx && EsDuplicado(dbEx)))
            {
                _db.ChangeTracker.Clear();

                var ganador = await BuscarValidadoAsync(id, hash, cancellationToken);
                if (ganador is not null)
                {
                    // Otra carga simultánea del mismo PDF ganó: respuesta idempotente, sin copia extra.
                    _archivos.Eliminar(almacenado.RutaAlmacenamiento);
                    return Ok(MapeosExpediente.ADto(ganador));
                }

                var recargado = await _db.Expedientes.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
                if (recargado is null || recargado.Estado != ExpedienteEstados.PendienteDocumentos || intento >= MaxIntentosGuardado)
                {
                    _archivos.Eliminar(almacenado.RutaAlmacenamiento);
                    return Conflict(new
                    {
                        mensaje = "El expediente cambió de estado mientras se cargaba el documento (por ejemplo, se envió). Consulte su estado.",
                        expedienteId = id,
                        estado = recargado?.Estado
                    });
                }

                expediente = recargado;
                await Task.Delay(Random.Shared.Next(20, 80) * intento, cancellationToken);
            }
            catch
            {
                // Compensación: sin fila en DB B no debe quedar un archivo huérfano en disco.
                _archivos.Eliminar(almacenado.RutaAlmacenamiento);
                throw;
            }
        }
    }

    /// <summary>Máximo de reintentos de guardado ante choques de concurrencia entre cargas simultáneas.</summary>
    private const int MaxIntentosGuardado = 5;

    private Task<ExpedienteDocumento?> BuscarValidadoAsync(Guid expedienteId, string hash, CancellationToken cancellationToken)
        => _db.ExpedienteDocumentos.AsNoTracking()
            .FirstOrDefaultAsync(d => d.ExpedienteId == expedienteId &&
                                      d.HashSha256 == hash &&
                                      d.EstadoRecepcion == EstadosRecepcionDocumento.Validado,
                cancellationToken);

    /// <summary>
    /// Retira un documento VALIDADO antes del envío (por ejemplo, se cargó el PDF equivocado).
    /// Elimina el archivo físico y conserva la fila con estado RETIRADO para auditoría.
    /// </summary>
    [HttpDelete("{id:guid}/documentos/{documentoId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Retirar(Guid id, Guid documentoId, CancellationToken cancellationToken)
    {
        var expediente = await _db.Expedientes.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        var documento = await _db.ExpedienteDocumentos
            .FirstOrDefaultAsync(d => d.Id == documentoId && d.ExpedienteId == id, cancellationToken);

        if (expediente is null || documento is null)
        {
            return NotFound(new { mensaje = "No existe el documento indicado en el expediente." });
        }

        if (expediente.Estado != ExpedienteEstados.PendienteDocumentos)
        {
            return Conflict(new { mensaje = "Solo se pueden retirar documentos antes de enviar el expediente.", estado = expediente.Estado });
        }

        if (documento.EstadoRecepcion != EstadosRecepcionDocumento.Validado)
        {
            return NoContent();
        }

        documento.EstadoRecepcion = EstadosRecepcionDocumento.Retirado;
        documento.ObservacionValidacion = "Retirado por el operador antes del envío.";
        expediente.SelloConcurrencia = Guid.NewGuid();

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { mensaje = "El expediente cambió de estado mientras se retiraba el documento." });
        }

        _archivos.Eliminar(documento.RutaAlmacenamiento);
        return NoContent();
    }

    private static bool EsDuplicado(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("IX_EXPEDIENTE_DOC_Unico_Validado", StringComparison.OrdinalIgnoreCase) == true
           || ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true;

    private static string Recortar(string? texto, int maximo)
    {
        texto ??= string.Empty;
        return texto.Length <= maximo ? texto : texto[..maximo];
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
