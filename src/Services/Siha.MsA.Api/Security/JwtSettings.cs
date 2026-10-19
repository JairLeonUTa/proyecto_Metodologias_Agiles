namespace Siha.MsA.Api.Security;

/// <summary>Parámetros de emisión de tokens JWT (US-01).</summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;

    public string Issuer { get; set; } = "SIHA-IA";

    public string Audience { get; set; } = "SIHA-IA-Clients";

    public int ExpiraMinutos { get; set; } = 60;
}
