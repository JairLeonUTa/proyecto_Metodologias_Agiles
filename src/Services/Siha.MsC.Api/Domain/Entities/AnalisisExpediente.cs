namespace Siha.MsC.Api.Domain.Entities;

/// <summary>
/// Solicitud que llegó con certificado SIHA (tabla ANALISIS_EXPEDIENTE, DB C): contexto leído del
/// certificado, docentes revisores asignados y estimación del agente guardada como JSON.
/// </summary>
public class AnalisisExpediente
{
    public Guid ExpedienteId { get; set; }

    public string CodigoExpediente { get; set; } = string.Empty;
    public string NumeroCertificado { get; set; } = string.Empty;
    public string EstudianteNombre { get; set; } = string.Empty;
    public string TipoTramite { get; set; } = string.Empty;
    public string RutaNormativa { get; set; } = string.Empty;
    public string InstitucionOrigen { get; set; } = string.Empty;
    public string CarreraOrigenCodigo { get; set; } = string.Empty;
    public string CarreraDestinoCodigo { get; set; } = string.Empty;

    /// <summary>Nombres completos tal como vienen en el certificado.</summary>
    public string CarreraOrigenNombre { get; set; } = string.Empty;
    public string CarreraDestinoNombre { get; set; } = string.Empty;

    /// <summary>Certificado SIHA leído (historial, solicitudes y revisores) serializado.</summary>
    public string CertificadoJson { get; set; } = "{}";

    /// <summary>
    /// Sílabos de la otra universidad adjuntados en la movilidad externa, leídos y serializados
    /// (lista de <c>Siha.Certificados.SilaboExterno</c>). Nulo cuando no se adjuntó ninguno.
    /// </summary>
    public string? SilabosExternosJson { get; set; }

    public string Revisor1Nombre { get; set; } = string.Empty;
    public string Revisor1Correo { get; set; } = string.Empty;
    public string Revisor2Nombre { get; set; } = string.Empty;
    public string Revisor2Correo { get; set; } = string.Empty;

    /// <summary>Ver <see cref="EstadosAnalisis"/>.</summary>
    public string Estado { get; set; } = EstadosAnalisis.Pendiente;

    /// <summary>Respuesta del agente (<c>AnalisisAutomaticoResponse</c>) serializada.</summary>
    public string? ResultadoJson { get; set; }

    /// <summary>Motivo del error cuando el análisis no pudo completarse.</summary>
    public string? Mensaje { get; set; }

    /// <summary>Reintentos automáticos hechos porque el agente (IA o reglas) no estaba disponible.</summary>
    public int Intentos { get; set; }

    /// <summary>Cuándo toca el próximo reintento automático (solo en estado PREPARANDO).</summary>
    public DateTimeOffset? ProximoIntentoUtc { get; set; }

    public DateTimeOffset CreadoUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? AnalizadoUtc { get; set; }

    public bool EsRevisor(string? correo) =>
        !string.IsNullOrWhiteSpace(correo) &&
        (string.Equals(Revisor1Correo, correo.Trim(), StringComparison.OrdinalIgnoreCase) ||
         string.Equals(Revisor2Correo, correo.Trim(), StringComparison.OrdinalIgnoreCase));
}

public static class EstadosAnalisis
{
    public const string Pendiente = "PENDIENTE";
    public const string Analizado = "ANALIZADO";
    public const string Error = "ERROR";

    /// <summary>
    /// El agente todavía no está listo (la primera vez descarga el modelo de IA) o el servicio de reglas
    /// no responde: la solicitud se reintenta sola, sin que nadie tenga que pulsar "Reintentar".
    /// </summary>
    public const string Preparando = "PREPARANDO";
}
