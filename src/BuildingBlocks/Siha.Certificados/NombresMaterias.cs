using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Siha.Certificados;

/// <summary>
/// Comparación de nombres de materias sin importar tildes, mayúsculas ni signos ("Física I" = "FISICA I").
/// Usa la misma regla que el agente para decidir si un nombre corresponde a una materia de la malla, y
/// además sugiere el nombre oficial más parecido cuando no coincide.
/// </summary>
public static class NombresMaterias
{
    private static readonly HashSet<string> Menores = new() { "y", "de", "del", "la", "las", "los", "el", "a", "en", "e", "para", "por", "con", "i", "ii", "iii", "iv" };

    /// <summary>"Álgebra Lineal" → "algebra lineal".</summary>
    public static string Clave(string texto)
    {
        var sinTildes = new string((texto ?? string.Empty).Normalize(NormalizationForm.FormD)
            .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray()).ToLowerInvariant();
        return string.Join(" ", Regex.Split(sinTildes, "[^a-z0-9]+").Where(p => p.Length > 0));
    }

    /// <summary>Mismo criterio que el agente: igual, o al menos 75 % de palabras en común.</summary>
    public static bool Coinciden(string a, string b)
    {
        var x = Clave(a);
        var y = Clave(b);
        if (x.Length == 0 || y.Length == 0) return false;
        return x == y || Jaccard(x, y) >= 0.75;
    }

    /// <summary>True si el nombre corresponde a alguna materia de la lista.</summary>
    public static bool Existe(string nombre, IEnumerable<string> malla) => malla.Any(m => Coinciden(m, nombre));

    /// <summary>
    /// Nombre oficial más parecido (por palabras en común y por letras), o null si ninguno se parece lo
    /// suficiente (menos de 50 %).
    /// </summary>
    public static string? MasParecida(string nombre, IEnumerable<string> malla)
    {
        var clave = Clave(nombre);
        if (clave.Length == 0) return null;

        var mejor = malla
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => (Nombre: m, Puntaje: Math.Max(Jaccard(clave, Clave(m)), Letras(clave, Clave(m)))))
            .OrderByDescending(x => x.Puntaje)
            .FirstOrDefault();
        return mejor.Nombre is not null && mejor.Puntaje >= 0.5 ? mejor.Nombre : null;
    }

    /// <summary>"ALGORITMOS Y LOGICA" → "Algoritmos y Logica", para mostrarlo bien en pantalla.</summary>
    public static string Titulo(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto) || texto.Any(char.IsLower)) return texto;
        var palabras = texto.ToLowerInvariant().Split(' ');
        return string.Join(" ", palabras.Select((p, i) =>
            p.Length == 0 ? p
            : Regex.IsMatch(p, "^(i|ii|iii|iv|v|vi)$") ? p.ToUpperInvariant()
            : i > 0 && Menores.Contains(p) ? p
            : char.ToUpperInvariant(p[0]) + p[1..]));
    }

    private static double Jaccard(string a, string b)
    {
        var x = a.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var y = b.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        if (x.Count == 0 || y.Count == 0) return 0;
        return (double)x.Intersect(y).Count() / x.Union(y).Count();
    }

    /// <summary>Parecido por letras (1 − distancia de edición relativa): tolera errores de tipeo.</summary>
    private static double Letras(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0) return 0;
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;
        for (var i = 1; i <= a.Length; i++)
            for (var j = 1; j <= b.Length; j++)
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
        return 1.0 - (double)d[a.Length, b.Length] / Math.Max(a.Length, b.Length);
    }
}
