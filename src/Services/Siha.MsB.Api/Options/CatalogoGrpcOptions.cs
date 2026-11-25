namespace Siha.MsB.Api.Options;

/// <summary>Dirección gRPC (h2c) de MS A para verificar los docentes revisores.</summary>
public class CatalogoGrpcOptions
{
    public const string SectionName = "CatalogoGrpc";

    public string Url { get; set; } = "http://localhost:5083";
}
