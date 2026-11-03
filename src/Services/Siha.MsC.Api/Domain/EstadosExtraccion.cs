namespace Siha.MsC.Api.Domain;

/// <summary>Estados de una extracción documental (US-07).</summary>
public static class EstadosExtraccion
{
    /// <summary>La extracción tiene datos dudosos o incompletos y requiere revisión humana.</summary>
    public const string PendienteConfirmacion = "PENDIENTE_CONFIRMACION";

    /// <summary>El operador revisó y confirmó créditos, horas y unidades; el cálculo puede avanzar.</summary>
    public const string Confirmada = "CONFIRMADA";

    /// <summary>El PDF no tiene texto interpretable; se deriva al flujo OCR/LLM de US-10.</summary>
    public const string PendienteOcrLlm = "PENDIENTE_OCR_LLM";

    /// <summary>Umbral de confianza para auto-confirmación (CA2 de US-07).</summary>
    public const decimal UmbralConfianzaAuto = 0.80m;
}
