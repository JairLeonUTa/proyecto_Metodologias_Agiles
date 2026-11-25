using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Siha.Certificados;

/// <summary>
/// Lee el sílabo (plan analítico) de una materia aprobada en otra universidad, para la movilidad externa.
///
/// A diferencia del certificado SIHA, el sílabo no tiene una plantilla fija: cada universidad usa la suya.
/// Por eso el lector es tolerante: busca las unidades como "Unidad N: nombre (horas)" (también Capítulo,
/// Módulo o Tema, con números arábigos o romanos) y toma como temas las líneas que siguen a cada unidad.
/// Lo único obligatorio es poder saber las unidades con sus temas y sus horas; si las horas no vienen
/// por unidad, se reparten desde las horas totales declaradas. Si ni eso se puede, el documento se
/// rechaza con un mensaje claro: el sistema nunca inventa datos.
/// </summary>
public static class LectorSilaboExterno
{
    private static readonly Regex RegexUnidad = new(
        @"^(?:unidad|capitulo|modulo|tema|bloque)\s+(?<numero>\d{1,2}|[ivxl]{1,6})\b\s*[:.\-–]?\s*",
        RegexOptions.Compiled);

    private static readonly Regex RegexHorasEnTitulo = new(
        @"[\(\[]?\s*(?<horas>\d{1,3})\s*h(?:oras?)?\.?\s*[\)\]]?\s*$",
        RegexOptions.Compiled);

    private static readonly string[] EtiquetasNombre = { "asignatura", "materia", "nombre de la asignatura", "nombre de la materia" };

    private static readonly string[] SeccionesFinales =
    {
        "bibliografia", "referencias", "evaluacion", "metodologia", "recursos", "firmas", "firma",
        "elaborado por", "revisado por", "aprobado por", "docente responsable", "fin del silabo", "fin del documento"
    };

    /// <summary>Lee el sílabo desde los bytes de un PDF. <paramref name="materia"/> es la materia del certificado a la que corresponde.</summary>
    public static ResultadoLecturaSilabo Leer(byte[] pdf, string materia)
    {
        if (pdf is null || pdf.Length < 5 || Encoding.ASCII.GetString(pdf, 0, 5) != "%PDF-")
        {
            return ResultadoLecturaSilabo.Invalido(new[] { $"El sílabo de «{materia}» no es un archivo PDF." });
        }

        IReadOnlyList<string> lineas;
        try
        {
            lineas = LectorCertificadoSiha.ExtraerLineas(pdf);
        }
        catch (Exception ex)
        {
            return ResultadoLecturaSilabo.Invalido(new[] { $"No se pudo leer el sílabo de «{materia}»: {ex.Message}" });
        }

        if (lineas.Count == 0)
        {
            return ResultadoLecturaSilabo.Invalido(new[]
            {
                $"El sílabo de «{materia}» no tiene texto seleccionable (parece escaneado). Pida a la otra universidad el documento digital, con texto."
            });
        }

        return LeerTexto(lineas, materia);
    }

    /// <summary>Interpreta el texto del sílabo (una entrada por línea).</summary>
    public static ResultadoLecturaSilabo LeerTexto(IEnumerable<string> lineasTexto, string materia)
    {
        var lineas = lineasTexto
            .Select(l => Regex.Replace(l ?? string.Empty, @"\s+", " ").Trim())
            .Where(l => l.Length > 0)
            .ToList();

        var textoCompleto = LectorCertificadoSiha.Normalizar(string.Join("\n", lineas));
        if (textoCompleto.Contains("certificado de homologacion"))
        {
            return ResultadoLecturaSilabo.Invalido(new[]
            {
                $"El archivo adjuntado para «{materia}» es un certificado de homologación, no el sílabo de la materia."
            });
        }

        string? nombre = null;
        int? creditos = null;
        int? horasTotales = null;
        var unidades = new List<(int Numero, string Nombre, int? Horas, List<string> Temas)>();
        var enSeccionFinal = false;

        foreach (var linea in lineas)
        {
            var normal = LectorCertificadoSiha.Normalizar(linea);

            if (SeccionesFinales.Any(s => normal.StartsWith(s, StringComparison.Ordinal)))
            {
                enSeccionFinal = true;
                continue;
            }

            var unidad = RegexUnidad.Match(normal);
            if (unidad.Success)
            {
                enSeccionFinal = false;
                var numero = ConvertirNumero(unidad.Groups["numero"].Value, unidades.Count + 1);
                // Normalizar conserva la longitud: el título se recorta de la línea original.
                var titulo = linea[unidad.Length..].Trim();
                int? horasUnidad = null;
                var horasTitulo = RegexHorasEnTitulo.Match(titulo);
                if (horasTitulo.Success && int.TryParse(horasTitulo.Groups["horas"].Value, out var h) && h > 0)
                {
                    horasUnidad = h;
                    titulo = titulo[..horasTitulo.Index].Trim().TrimEnd('(', '[', '-', '–', ':', ',', '.');
                }

                if (titulo.Length > 0)
                {
                    unidades.Add((numero, titulo, horasUnidad, new List<string>()));
                }

                continue;
            }

            if (ValorDeEtiqueta(linea, normal, EtiquetasNombre) is { } valorNombre && nombre is null)
            {
                nombre = valorNombre;
                continue;
            }

            var creditosLeidos = Regex.Match(normal, @"^creditos?\s*[:.\-]?\s*(\d{1,2})\b");
            if (creditosLeidos.Success && creditos is null)
            {
                creditos = int.Parse(creditosLeidos.Groups[1].Value, CultureInfo.InvariantCulture);
                continue;
            }

            var horasLeidas = Regex.Match(normal, @"^(?:horas totales|total de horas|horas|carga horaria)\s*[:.\-]?\s*(\d{1,4})\b");
            if (horasLeidas.Success && horasTotales is null)
            {
                horasTotales = int.Parse(horasLeidas.Groups[1].Value, CultureInfo.InvariantCulture);
                continue;
            }

            // Cualquier otra línea dentro de una unidad son sus temas.
            if (!enSeccionFinal && unidades.Count > 0)
            {
                unidades[^1].Temas.Add(linea.TrimStart('-', '•', '·', '*', ' '));
            }
        }

        var errores = new List<string>();
        if (unidades.Count == 0)
        {
            errores.Add($"En el sílabo de «{materia}» no se encontraron unidades ('Unidad 1: ...', 'Unidad 2: ...'). " +
                        "Adjunte el sílabo o plan analítico con las unidades y sus temas.");
            return ResultadoLecturaSilabo.Invalido(errores);
        }

        foreach (var sinTemas in unidades.Where(u => u.Temas.Count == 0))
        {
            errores.Add($"En el sílabo de «{materia}», la unidad {sinTemas.Numero} «{sinTemas.Nombre}» no tiene temas debajo.");
        }

        // Horas por unidad: las declaradas; las que faltan se reparten desde las horas totales.
        var declaradas = unidades.Where(u => u.Horas is > 0).Sum(u => u.Horas!.Value);
        var sinHoras = unidades.Count(u => u.Horas is null or <= 0);
        var horasRepartidas = 0;
        if (sinHoras > 0)
        {
            var restantes = (horasTotales ?? 0) - declaradas;
            if (restantes >= sinHoras)
            {
                horasRepartidas = restantes / sinHoras;
            }
            else
            {
                errores.Add($"El sílabo de «{materia}» no indica las horas de cada unidad ni las horas totales de la materia. " +
                            "Sin las horas no se puede comparar; pida el sílabo completo.");
            }
        }

        if (errores.Count > 0) return ResultadoLecturaSilabo.Invalido(errores);

        var lista = unidades
            .Select(u => new UnidadSilabo(u.Numero, u.Nombre, u.Horas is > 0 ? u.Horas.Value : horasRepartidas, string.Join(" ", u.Temas)))
            .OrderBy(u => u.Numero)
            .ToList();

        return ResultadoLecturaSilabo.Valido(new SilaboExterno(
            materia.Trim(), nombre, creditos, lista.Sum(u => u.Horas), lista));
    }

    private static string? ValorDeEtiqueta(string original, string normal, string[] etiquetas)
    {
        foreach (var etiqueta in etiquetas.OrderByDescending(e => e.Length))
        {
            var coincidencia = Regex.Match(normal, @"^" + Regex.Escape(etiqueta) + @"\s*:\s*");
            if (coincidencia.Success)
            {
                var valor = original[coincidencia.Length..].Trim();
                return valor.Length > 0 ? valor : null;
            }
        }

        return null;
    }

    private static int ConvertirNumero(string valor, int fallback)
    {
        if (int.TryParse(valor, out var n) && n > 0) return n;

        return valor.ToUpperInvariant() switch
        {
            "I" => 1, "II" => 2, "III" => 3, "IV" => 4, "V" => 5,
            "VI" => 6, "VII" => 7, "VIII" => 8, "IX" => 9, "X" => 10,
            _ => fallback
        };
    }
}
