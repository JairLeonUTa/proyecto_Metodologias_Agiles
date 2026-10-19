namespace Siha.Contracts.Events;

/// <summary>
/// Contrato formal del evento <c>SolicitudCreadaEvent</c> intercambiado entre el Microservicio B
/// y el Microservicio C (Sprint 1 - US-06, tarea S1-J07).
///
/// Reglas del contrato (CA1 de US-06):
///  - Incluye el identificador del expediente y las referencias mínimas para procesarlo.
///  - NO adjunta los archivos binarios (PDF) al mensaje; solo referencias (DocumentoIds / rutas).
///  - Conserva siempre el <see cref="CorrelationId"/> para la trazabilidad extremo a extremo (CA3).
/// </summary>
public sealed record SolicitudCreadaEvent
{
    /// <summary>Identificador del expediente creado en SQL Server DB B.</summary>
    public Guid ExpedienteId { get; init; }

    /// <summary>Identificador de correlación extremo a extremo (conservado en cada reintento).</summary>
    public Guid CorrelationId { get; init; }

    /// <summary>Código legible del expediente (prefijo + secuencia).</summary>
    public string CodigoExpediente { get; init; } = string.Empty;

    /// <summary>INTERNO o EXTERNO.</summary>
    public string TipoTramite { get; init; } = string.Empty;

    /// <summary>CAU-P-011 (cambio de carrera) o CAU-P-122 (movilidad).</summary>
    public string RutaNormativa { get; init; } = string.Empty;

    /// <summary>Código de la carrera de origen (malla origen).</summary>
    public string CarreraOrigenCodigo { get; init; } = string.Empty;

    /// <summary>Código de la carrera receptora (malla destino).</summary>
    public string CarreraDestinoCodigo { get; init; } = string.Empty;

    /// <summary>Identificadores de los documentos PDF asociados (referencias, no binarios).</summary>
    public IReadOnlyCollection<Guid> DocumentoIds { get; init; } = Array.Empty<Guid>();

    /// <summary>Códigos de las asignaturas de origen seleccionadas para homologación.</summary>
    public IReadOnlyCollection<string> AsignaturasOrigen { get; init; } = Array.Empty<string>();

    /// <summary>Estado del expediente al momento de publicar (PENDIENTE_CONTENIDOS).</summary>
    public string Estado { get; init; } = ExpedienteEstados.PendienteContenidos;

    /// <summary>Marca temporal de creación en UTC.</summary>
    public DateTimeOffset FechaCreacionUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Versión del contrato del evento.</summary>
    public string Version { get; init; } = "1.0";
}
