namespace Siha.MsB.Api.Security;

/// <summary>Parámetros de validación de tokens JWT (compartidos con el Microservicio A).</summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;

    public string Issuer { get; set; } = "SIHA-IA";

    public string Audience { get; set; } = "SIHA-IA-Clients";
}
