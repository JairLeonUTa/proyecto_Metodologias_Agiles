namespace Siha.MsC.Api.Security;

/// <summary>Configuración para validar los JWT emitidos por el Microservicio A.</summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;

    public string Issuer { get; set; } = "SIHA-IA";

    public string Audience { get; set; } = "SIHA-IA-Clients";
}
