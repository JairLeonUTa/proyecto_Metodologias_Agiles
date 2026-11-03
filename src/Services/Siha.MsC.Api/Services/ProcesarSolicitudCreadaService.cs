using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Siha.Contracts.Events;
using Siha.MsC.Api.Data;
using Siha.MsC.Api.Domain.Entities;

namespace Siha.MsC.Api.Services;

public enum ResultadoProcesamiento
{
    Procesado,
    Duplicado
}

/// <summary>
/// Procesa <see cref="SolicitudCreadaEvent"/> una sola vez por expediente.
///
/// 1. Comprueba PROCESSED_EVENTS (idempotencia por ExpedienteId = MessageId).
/// 2. Verifica cada PDF contra el volumen compartido (ruta, tamaño y SHA-256).
/// 3. Solo si todo es válido registra el evento como procesado.
///
/// Si la verificación falla, no se registra nada: MassTransit reintenta el mensaje.
/// </summary>
public class ProcesarSolicitudCreadaService
{
    private readonly SihaDbContextC _db;
    private readonly IAlmacenDocumentos _almacen;
    private readonly ILogger<ProcesarSolicitudCreadaService> _logger;

    public ProcesarSolicitudCreadaService(
        SihaDbContextC db,
        IAlmacenDocumentos almacen,
        ILogger<ProcesarSolicitudCreadaService> logger)
    {
        _db = db;
        _almacen = almacen;
        _logger = logger;
    }

    public async Task<ResultadoProcesamiento> ProcesarAsync(
        SolicitudCreadaEvent evento,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        if (evento is null)
            throw new InvalidOperationException("El evento SolicitudCreadaEvent es nulo.");

        if (evento.ExpedienteId == Guid.Empty)
            throw new InvalidOperationException("El evento no incluye un ExpedienteId válido.");

        var yaProcesado = await _db.EventosProcesados
            .AsNoTracking()
            .AnyAsync(e => e.ExpedienteId == evento.ExpedienteId, cancellationToken);

        if (yaProcesado)
        {
            _logger.LogInformation(
                "Evento duplicado ignorado. ExpedienteId={ExpedienteId} MessageId={MessageId} CorrelationId={CorrelationId}",
                evento.ExpedienteId, messageId, evento.CorrelationId);
            return ResultadoProcesamiento.Duplicado;
        }

        if (evento.Documentos is null || evento.Documentos.Count == 0)
        {
            throw new InvalidOperationException(
                $"El expediente {evento.ExpedienteId} no trae referencias de documentos verificables.");
        }

        var problemas = new List<string>();
        foreach (var documento in evento.Documentos)
        {
            var verificacion = await _almacen.VerificarAsync(documento, cancellationToken);
            if (!verificacion.Disponible)
            {
                problemas.Add($"Documento {documento.DocumentoId}: {verificacion.Motivo}");
            }
        }

        if (problemas.Count > 0)
        {
            throw new InvalidOperationException(
                $"No se pudo verificar los documentos del expediente {evento.ExpedienteId}: {string.Join(" | ", problemas)}");
        }

        _db.EventosProcesados.Add(new EventoProcesado
        {
            ExpedienteId = evento.ExpedienteId,
            MessageId = messageId,
            CorrelationId = evento.CorrelationId,
            TipoEvento = nameof(SolicitudCreadaEvent),
            RecibidoUtc = DateTimeOffset.UtcNow
        });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (EsDuplicado(ex))
        {
            // Dos entregas simultáneas del mismo expediente: la clave primaria decide.
            _logger.LogInformation(
                "Evento duplicado detectado por la clave de idempotencia. ExpedienteId={ExpedienteId}",
                evento.ExpedienteId);
            return ResultadoProcesamiento.Duplicado;
        }

        _logger.LogInformation(
            "SolicitudCreadaEvent procesado. ExpedienteId={ExpedienteId} MessageId={MessageId} Documentos={Documentos}",
            evento.ExpedienteId, messageId, evento.Documentos.Count);

        return ResultadoProcesamiento.Procesado;
    }

    private static bool EsDuplicado(DbUpdateException ex)
        => ex.InnerException is SqlException { Number: 2601 or 2627 };
}
