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
}
