using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Siha.Contracts.Events;
using Siha.MsC.Api.Data;
using Siha.MsC.Api.Domain;
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
    private readonly IExtractorPdfService _extractor;
    private readonly ILogger<ProcesarSolicitudCreadaService> _logger;
    private readonly ILectorCertificadosAlmacen? _certificados;

    public ProcesarSolicitudCreadaService(
        SihaDbContextC db,
        IAlmacenDocumentos almacen,
        IExtractorPdfService extractor,
        ILogger<ProcesarSolicitudCreadaService> logger,
        ILectorCertificadosAlmacen? certificados = null)
    {
        _db = db;
        _almacen = almacen;
        _extractor = extractor;
        _logger = logger;
        _certificados = certificados;
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

        foreach (var documento in evento.Documentos)
        {
            if (await _db.Extracciones.AnyAsync(x => x.DocumentoId == documento.DocumentoId, cancellationToken))
                continue;

            // Certificado SIHA (SIHA-CH-01): documento preaprobado con todas las materias. Cada materia
            // queda como una extracción CONFIRMADA con confianza 1.00; no requiere revisión del operador.
            var certificado = _certificados is null
                ? null
                : await _certificados.LeerAsync(documento.RutaAlmacenamiento, cancellationToken);
            if (certificado is not null)
            {
                foreach (var asignatura in certificado.Asignaturas)
                {
                    _db.Extracciones.Add(new Extraccion
                    {
                        ExpedienteId = evento.ExpedienteId,
                        DocumentoId = documento.DocumentoId,
                        CodigoAsignaturaOrigen = asignatura.Codigo,
                        NombreAsignaturaOrigen = asignatura.Nombre,
                        Creditos = asignatura.Creditos,
                        Horas = asignatura.HorasTotales,
                        Confianza = 1.00m,
                        Estado = EstadosExtraccion.Confirmada,
                        ConfirmadoUtc = DateTimeOffset.UtcNow,
                        ConfirmadoPor = "CERTIFICADO_SIHA",
                        Observacion = $"Datos del certificado SIHA {certificado.NumeroCertificado} (documento preaprobado, plantilla SIHA-CH-01).",
                        FechaAprobacion = asignatura.FechaAprobacion,
                        Nota = asignatura.Nota,
                        Semestre = asignatura.Semestre,
                        Unidades = asignatura.Unidades.Select(u => new ExtraccionUnidad
                        {
                            Numero = u.Numero,
                            Nombre = u.Nombre,
                            Horas = u.Horas,
                            Contenidos = u.Contenidos
                        }).ToList()
                    });
                }

                // Contexto para el agente: el análisis se ejecuta en cuanto se confirma este procesamiento.
                if (!await _db.AnalisisExpedientes.AnyAsync(a => a.ExpedienteId == evento.ExpedienteId, cancellationToken))
                {
                    var r1 = certificado.Revisores.ElementAtOrDefault(0);
                    var r2 = certificado.Revisores.ElementAtOrDefault(1);
                    _db.AnalisisExpedientes.Add(new AnalisisExpediente
                    {
                        ExpedienteId = evento.ExpedienteId,
                        CodigoExpediente = evento.CodigoExpediente,
                        NumeroCertificado = certificado.NumeroCertificado,
                        EstudianteNombre = certificado.EstudianteNombre,
                        TipoTramite = certificado.TipoTramite,
                        RutaNormativa = certificado.RutaNormativa,
                        InstitucionOrigen = certificado.InstitucionOrigen,
                        CarreraOrigenCodigo = certificado.CarreraOrigenCodigo,
                        CarreraDestinoCodigo = certificado.CarreraDestinoCodigo,
                        Revisor1Nombre = r1?.Nombre ?? string.Empty,
                        Revisor1Correo = r1?.Correo ?? string.Empty,
                        Revisor2Nombre = r2?.Nombre ?? string.Empty,
                        Revisor2Correo = r2?.Correo ?? string.Empty
                    });
                }

                _logger.LogInformation(
                    "Certificado SIHA {Certificado} leído: {Asignaturas} materia(s) confirmada(s). ExpedienteId={ExpedienteId}",
                    certificado.NumeroCertificado, certificado.Asignaturas.Count, evento.ExpedienteId);
                continue;
            }

            var codigo = evento.AsignaturasOrigen.Count == 1 ? evento.AsignaturasOrigen.First() : null;
            var resultado = await _extractor.ExtraerDeArchivoAsync(
                documento.RutaAlmacenamiento, codigo, cancellationToken);

            // El PDF escaneado no se puede procesar: el pipeline OCR/LLM no está operativo.
            // Se cierra la extracción en CANCELADA para no dejarla pendiente para siempre.
            var sinTexto = resultado.EstadoSugerido == EstadosExtraccion.PendienteOcrLlm;

            if (sinTexto)
            {
                _logger.LogWarning(
                    "Extracción cancelada: el documento {DocumentoId} no tiene capa de texto y el OCR/LLM no está operativo. ExpedienteId={ExpedienteId}",
                    documento.DocumentoId, evento.ExpedienteId);
            }

            _db.Extracciones.Add(new Extraccion
            {
                ExpedienteId = evento.ExpedienteId,
                DocumentoId = documento.DocumentoId,
                CodigoAsignaturaOrigen = resultado.CodigoAsignaturaOrigen,
                NombreAsignaturaOrigen = resultado.NombreAsignaturaOrigen,
                Creditos = resultado.Creditos,
                Horas = resultado.Horas,
                Confianza = resultado.Confianza,
                Estado = sinTexto
                    ? EstadosExtraccion.Cancelada
                    : EstadosExtraccion.PendienteConfirmacion,
                Observacion = sinTexto
                    ? "El PDF no tiene capa de texto y el análisis OCR/LLM no está operativo: extracción cancelada sin datos. Cargue un PDF con texto seleccionable para continuar."
                    : resultado.Observacion,
                Unidades = resultado.Unidades.Select(u => new ExtraccionUnidad
                {
                    Numero = u.Numero,
                    Nombre = u.Nombre,
                    Horas = u.Horas,
                    Contenidos = u.Contenidos
                }).ToList()
            });
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
