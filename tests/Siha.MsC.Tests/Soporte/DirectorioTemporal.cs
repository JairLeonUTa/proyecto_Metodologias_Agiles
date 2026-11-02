namespace Siha.MsC.Tests.Soporte;

/// <summary>Directorio temporal aislado por prueba; se elimina al terminar.</summary>
public sealed class DirectorioTemporal : IDisposable
{
    public DirectorioTemporal()
    {
        Ruta = Path.Combine(Path.GetTempPath(), "siha-msc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Ruta);
    }

    public string Ruta { get; }

    public string CrearArchivo(string nombreRelativo, byte[] contenido)
    {
        var ruta = Path.Combine(Ruta, nombreRelativo);
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        File.WriteAllBytes(ruta, contenido);
        return ruta;
    }

    public void Dispose()
    {
        if (Directory.Exists(Ruta))
            Directory.Delete(Ruta, recursive: true);
    }
}
