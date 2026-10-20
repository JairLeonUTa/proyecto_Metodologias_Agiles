using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Siha.Contracts;
using Siha.Contracts.Events;
using Siha.MsB.Api.Data;
using Siha.MsB.Api.Domain;
using Siha.MsB.Api.Domain.Entities;
using Siha.MsB.Api.Events;
using Siha.MsB.Api.Options;

namespace Siha.MsB.Api.Services;

/// <summary>Origen de la solicitud de envío: el operador o un reintento (manual o automático).</summary>
public enum ModoEnvio
{
    /// <summary>Primer envío del borrador (<c>POST /api/expedientes/{id}/enviar</c>).</summary>
    Envio,

    /// <summary>Reintento tras un fallo del bus (<c>/republicar</c> o el reintento automático).</summary>
    Reintento
}

public enum TipoResultadoEnvio
{
    Publicado,
    YaPublicado,
    FalloPublicacion,
    RequisitosIncumplidos,
    EstadoNoPermitido,
    Conflicto,
    NoEncontrado
}

public sealed record ResultadoEnvio(
    TipoResultadoEnvio Tipo,
    Expediente? Expediente,
    string Mensaje,
    IReadOnlyList<string> Detalles)
{
    public bool Publicado => Tipo is TipoResultadoEnvio.Publicado or TipoResultadoEnvio.YaPublicado;
}

/// <summary>
/// Orquesta el paso de borrador a publicado (flujo acordado para el Sprint 1):
///
///  1. El expediente se registra como <c>PENDIENTE_DOCUMENTOS</c> (sin evento).
///  2. Los PDF se cargan y validan en el servidor (<c>POST /api/expedientes/{id}/documentos</c>).
///  3. Al enviar, este servicio comprueba en disco cada documento VALIDADO (existencia, tamaño y
///     SHA-256 recalculado). Solo si todos siguen íntegros y se cumple el mínimo documental reserva el expediente con
///     concurrencia optimista (<c>PUBLICACION_EN_CURSO</c>) y publica <see cref="SolicitudCreadaEvent"/>
///     con las referencias verificadas. Ningún dato del cliente llega al bus.
///  4. Si el bus falla, queda <c>REGISTRADO_PENDIENTE_PUBLICACION</c> y se reintenta conservando el
///     CorrelationId; un expediente ya publicado nunca se vuelve a publicar.
/// </summary>
public class ExpedienteEnvioService
{
    private readonly SihaDbContextB _db;
    private readonly IArchivoStorageService _archivos;
    private readonly IExpedienteEventPublisher _publisher;
    private readonly ArchivosOptions _opcionesArchivos;
    private readonly RepublicacionOptions _opcionesRepublicacion;
    private readonly ILogger<ExpedienteEnvioService> _logger;

    public ExpedienteEnvioService(
        SihaDbContextB db,
        IArchivoStorageService archivos,
        IExpedienteEventPublisher publisher,
        IOptions<ArchivosOptions> opcionesArchivos,
        IOptions<RepublicacionOptions> opcionesRepublicacion,
        ILogger<ExpedienteEnvioService> logger)
    {
        _db = db;
        _archivos = archivos;
        _publisher = publisher;
        _opcionesArchivos = opcionesArchivos.Value;
        _opcionesRepublicacion = opcionesRepublicacion.Value;
        _logger = logger;
    }

    public async Task<ResultadoEnvio> EnviarAsync(Guid expedienteId, ModoEnvio modo, CancellationToken cancellationToken = default)
    {
        var expediente = await _db.Expedientes
            .Include(e => e.Documentos)
            .Include(e => e.Asignaturas)
            .FirstOrDefaultAsync(e => e.Id == expedienteId, cancellationToken);

        if (expediente is null)
        {
            return Resultado(TipoResultadoEnvio.NoEncontrado, null, $"No existe el expediente '{expedienteId}'.");
        }

        // Idempotencia: un expediente publicado nunca se publica de nuevo (sin mensajes duplicados).
        if (expediente.EventoPublicado)
        {
            return Resultado(TipoResultadoEnvio.YaPublicado, expediente,
                "El expediente ya fue publicado; no se vuelve a enviar el evento.");
        }

        var permitido = EstadoPermiteEnvio(expediente, modo);
        if (permitido is not null)
        {
            return permitido;
        }

        // Verificación en disco de los documentos aceptados: no se confía en lo que envió el cliente.
        var (verificados, problemas) = await VerificarDocumentosAsync(expediente, cancellationToken);
        var minimo = Math.Max(1, _opcionesArchivos.MinimoDocumentosRequeridos);

        if (problemas.Count > 0 || verificados.Count < minimo)
        {
            // Si el expediente venía de un fallo de publicación y sus archivos ya no están, vuelve a
            // borrador para que el operador los cargue de nuevo.
            expediente.Estado = ExpedienteEstados.PendienteDocumentos;
            expediente.SelloConcurrencia = Guid.NewGuid();

            if (!await GuardarConControlDeConcurrenciaAsync(cancellationToken))
            {
                return Resultado(TipoResultadoEnvio.Conflicto, expediente,
                    "El expediente fue modificado por otra operación. Consulte su estado e intente de nuevo.");
            }

            var detalles = problemas.ToList();
            if (verificados.Count < minimo)
            {
                detalles.Insert(0, $"Se requieren al menos {minimo} documento(s) PDF validado(s) y disponible(s); hay {verificados.Count}.");
            }

            _logger.LogWarning(
                "Envío rechazado por requisitos documentales. ExpedienteId={ExpedienteId} CorrelationId={CorrelationId} Verificados={Verificados} Minimo={Minimo}",
                expediente.Id, expediente.CorrelationId, verificados.Count, minimo);

            return new ResultadoEnvio(TipoResultadoEnvio.RequisitosIncumplidos, expediente,
                "El expediente no cumple los requisitos documentales para publicarse.", detalles);
        }

        // Reserva con concurrencia optimista: solo una petición puede pasar a PUBLICACION_EN_CURSO.
        expediente.Estado = ExpedienteEstados.PublicacionEnCurso;
        expediente.FechaUltimoIntentoUtc = DateTimeOffset.UtcNow;
        expediente.SelloConcurrencia = Guid.NewGuid();

        if (!await GuardarConControlDeConcurrenciaAsync(cancellationToken))
        {
            return Resultado(TipoResultadoEnvio.Conflicto, expediente,
                "Otra solicitud está enviando este expediente; no se publicará dos veces.");
        }

        var evento = ConstruirEvento(expediente, verificados);
        var (publicado, error) = await _publisher.PublicarAsync(evento, CancellationToken.None);

        expediente.IntentosPublicacion += 1;
        expediente.SelloConcurrencia = Guid.NewGuid();

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
            expediente.UltimoErrorPublicacion = Recortar(error, 1000);
        }

        // Este guardado no debe cancelarse: el evento ya salió (o falló) y el estado debe reflejarlo.
        await _db.SaveChangesAsync(CancellationToken.None);

        return publicado
            ? Resultado(TipoResultadoEnvio.Publicado, expediente,
                $"Expediente publicado con {verificados.Count} documento(s) verificado(s).")
            : Resultado(TipoResultadoEnvio.FalloPublicacion, expediente,
                "Los documentos se verificaron, pero la publicación en el bus falló; el expediente quedó en estado recuperable y se reintentará conservando el CorrelationId.");
    }

    /// <summary>Construye el evento solo con los documentos verificados en disco por el servidor.</summary>
    public static SolicitudCreadaEvent ConstruirEvento(Expediente expediente, IReadOnlyCollection<ExpedienteDocumento> verificados) => new()
    {
        ExpedienteId = expediente.Id,
        CorrelationId = expediente.CorrelationId,
        CodigoExpediente = expediente.Codigo,
        TipoTramite = expediente.TipoTramite,
        RutaNormativa = expediente.RutaNormativa,
        CarreraOrigenCodigo = expediente.CarreraOrigenCodigo,
        CarreraDestinoCodigo = expediente.CarreraDestinoCodigo,
        DocumentoIds = verificados.Select(d => d.Id).ToList(),
        Documentos = verificados
            .Select(d => new DocumentoReferencia
            {
                DocumentoId = d.Id,
                RutaAlmacenamiento = d.RutaAlmacenamiento,
                HashSha256 = d.HashSha256,
                TamanoBytes = d.TamanoBytes
            })
            .ToList(),
        AsignaturasOrigen = expediente.Asignaturas.Select(a => a.AsignaturaOrigenCodigo).ToList(),
        Estado = ExpedienteEstados.PendienteContenidos,
        FechaCreacionUtc = expediente.FechaCreacionUtc
    };

    private ResultadoEnvio? EstadoPermiteEnvio(Expediente expediente, ModoEnvio modo)
    {
        switch (expediente.Estado)
        {
            case ExpedienteEstados.PendienteDocumentos when modo == ModoEnvio.Envio:
            case ExpedienteEstados.FalloPublicacion:
                return null;

            case ExpedienteEstados.PendienteDocumentos:
                return Resultado(TipoResultadoEnvio.EstadoNoPermitido, expediente,
                    "El expediente aún no se ha enviado; use POST /api/expedientes/{id}/enviar.");

            case ExpedienteEstados.PublicacionEnCurso:
                var limite = DateTimeOffset.UtcNow.AddSeconds(-_opcionesRepublicacion.EnCursoAbandonadoSegundos);
                if (modo == ModoEnvio.Reintento && expediente.FechaUltimoIntentoUtc is { } intento && intento < limite)
                {
                    // Envío abandonado (p. ej. el proceso se reinició a mitad de la publicación).
                    return null;
                }

                return Resultado(TipoResultadoEnvio.Conflicto, expediente,
                    "El expediente se está enviando en este momento; no se publicará dos veces.");

            default:
                return Resultado(TipoResultadoEnvio.EstadoNoPermitido, expediente,
                    $"El expediente está en estado '{expediente.Estado}' y no admite envío.");
        }
    }

    private async Task<(List<ExpedienteDocumento> Verificados, List<string> Problemas)> VerificarDocumentosAsync(
        Expediente expediente,
        CancellationToken cancellationToken)
    {
        var verificados = new List<ExpedienteDocumento>();
        var problemas = new List<string>();

        foreach (var documento in expediente.Documentos
                     .Where(d => d.EstadoRecepcion == EstadosRecepcionDocumento.Validado)
                     .OrderBy(d => d.FechaCargaUtc))
        {
            var verificacion = await _archivos.VerificarAsync(
                documento.RutaAlmacenamiento, documento.HashSha256, documento.TamanoBytes, cancellationToken);

            if (verificacion.Disponible)
            {
                verificados.Add(documento);
                continue;
            }

            documento.EstadoRecepcion = EstadosRecepcionDocumento.NoDisponible;
            documento.ObservacionValidacion = Recortar(verificacion.Motivo, 500);

            _logger.LogWarning(
                "Documento no disponible al verificar antes de publicar. ExpedienteId={ExpedienteId} DocumentoId={DocumentoId} Motivo={Motivo}",
                expediente.Id, documento.Id, verificacion.Motivo);
        }

        // NO_DISPONIBLE no desaparece de los requisitos al reintentar. La recarga del mismo
        // contenido (hash y tamaño del servidor) sustituye la referencia fallida para el envío.
        // ponytail: búsqueda O(n²) para pocos adjuntos; usar HashSet de (hash, tamaño) si aumenta el volumen.
        foreach (var documento in expediente.Documentos.Where(d =>
                     d.EstadoRecepcion == EstadosRecepcionDocumento.NoDisponible &&
                     !verificados.Any(v => v.HashSha256 == d.HashSha256 && v.TamanoBytes == d.TamanoBytes)))
        {
            problemas.Add($"'{documento.NombreArchivo}' ({documento.Id}): {documento.ObservacionValidacion} Recargue el mismo PDF antes de enviar.");
        }

        return (verificados, problemas);
    }

    private async Task<bool> GuardarConControlDeConcurrenciaAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    private static ResultadoEnvio Resultado(TipoResultadoEnvio tipo, Expediente? expediente, string mensaje)
        => new(tipo, expediente, mensaje, Array.Empty<string>());

    private static string? Recortar(string? texto, int maximo)
        => texto is null || texto.Length <= maximo ? texto : texto[..maximo];
}
