using System.Text.Json;
using Siha.Certificados;

namespace Siha.MsB.Tests.Soporte;

/// <summary>Mallas reales de la UTA (los mismos JSON con los que MS A siembra el catálogo).</summary>
public static class CatalogoReal
{
    private static readonly Lazy<Dictionary<string, MateriaCatalogo[]>> Cache = new(Cargar);

    public static IReadOnlyDictionary<string, MateriaCatalogo[]> Mallas() => Cache.Value;

    public static MateriaCatalogo[] Malla(string codigo) => Cache.Value[codigo];

    private static Dictionary<string, MateriaCatalogo[]> Cargar()
    {
        var carpeta = Path.Combine(Pruebas.RaizRepositorio(), "src", "Services", "Siha.MsA.Api", "Data", "CatalogoUta");
        var mallas = new Dictionary<string, MateriaCatalogo[]>();
        foreach (var archivo in Directory.GetFiles(carpeta, "*.json"))
        {
            using var json = JsonDocument.Parse(File.ReadAllText(archivo));
            var raiz = json.RootElement;
            mallas[raiz.GetProperty("carrera").GetString()!] = raiz.GetProperty("asignaturas").EnumerateArray()
                .Select(a => new MateriaCatalogo(
                    a.GetProperty("nombre").GetString()!,
                    a.GetProperty("nivel").GetInt32(),
                    a.GetProperty("creditos").GetInt32(),
                    string.Join(" ", a.TryGetProperty("unidades", out var unidades) && unidades.ValueKind == JsonValueKind.Array
                        ? unidades.EnumerateArray().Select(u => $"{Texto(u, "nombre")} {Texto(u, "contenidos")}")
                        : Array.Empty<string>())))
                .ToArray();
        }

        return mallas;
    }

    private static string Texto(JsonElement e, string propiedad)
        => e.TryGetProperty(propiedad, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : string.Empty;
}
