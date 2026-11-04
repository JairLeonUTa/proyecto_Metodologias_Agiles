using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Siha.MsC.Api.Domain;
using Siha.MsC.Api.Options;

namespace Siha.MsC.Api.Services;

/// <summary>
/// Implementación del extractor de PDF para textos nativos y detector de escaneados (US-07, S2-P01, S2-P02).
/// Garantiza la regla de negocio:
/// - Confianza >= 0.80 y datos completos -> CONFIRMADA / auto-validada
/// - Confianza < 0.80 o datos obligatorios faltantes -> PENDIENTE_CONFIRMACION
/// - Sin capa de texto / escaneado -> PENDIENTE_OCR_LLM sin inventar datos
/// </summary>
public sealed class ExtractorPdfService : IExtractorPdfService
{
    private readonly string _raizAlmacen;
    private readonly ILogger<ExtractorPdfService> _logger;

    public ExtractorPdfService(
        IOptions<AlmacenOptions> almacenOptions,
        ILogger<ExtractorPdfService> logger)
    {
        _raizAlmacen = Path.GetFullPath(almacenOptions.Value.RutaAlmacenamiento);
        _logger = logger;
    }

    public Task<ExtraccionResultado> ExtraerDeTextoAsync(
        string? contenidoTexto,
        bool tieneCapaTexto,
        string? codigoAsignaturaFallback = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!tieneCapaTexto || EsPdfEscaneado(contenidoTexto))
        {
            _logger.LogInformation("Documento identificado como escaneado o sin capa de texto interpretable.");
            return Task.FromResult(new ExtraccionResultado(
                CodigoAsignaturaOrigen: codigoAsignaturaFallback ?? string.Empty,
                NombreAsignaturaOrigen: string.Empty,
                Creditos: null,
                Horas: null,
                Confianza: 0.0m,
                EstadoSugerido: EstadosExtraccion.PendienteOcrLlm,
                EsEscaneado: true,
                RequiereConfirmacionHumana: true,
                Unidades: Array.Empty<UnidadExtraida>(),
                CamposFaltantes: new[] { "Capa de texto ausente", "Creditos", "Horas", "Unidades" },
                MetodoExtraccion: "DetectorEscaneado",
                Observacion: "El documento carece de texto interpretable; derivado a flujo OCR + LLM (US-10)."
            ));
        }

        var texto = contenidoTexto ?? string.Empty;

        // 1. Extraer código y nombre de asignatura
        var codigo = ExtraerCodigoAsignatura(texto) ?? codigoAsignaturaFallback ?? string.Empty;
        var nombre = ExtraerNombreAsignatura(texto) ?? string.Empty;

        // 2. Extraer créditos y horas
        var creditos = ExtraerCreditos(texto);
        var horas = ExtraerHoras(texto);

        // 3. Extraer unidades temáticas
        var unidades = ExtraerUnidades(texto);

        // 4. Evaluar campos faltantes
        var camposFaltantes = new List<string>();
        if (string.IsNullOrWhiteSpace(nombre)) camposFaltantes.Add("NombreAsignatura");
        if (string.IsNullOrWhiteSpace(codigo)) camposFaltantes.Add("CodigoAsignatura");
        if (!creditos.HasValue) camposFaltantes.Add("Creditos");
        if (!horas.HasValue) camposFaltantes.Add("Horas");
        if (unidades.Count == 0) camposFaltantes.Add("Unidades");

        // 5. Calcular confianza objetiva
        var confianza = CalcularConfianza(creditos, horas, unidades, nombre, codigo);

        // 6. Decidir estado según umbral normativo 0.80 (CA2 de US-07)
        var esAutoAprobable = confianza >= EstadosExtraccion.UmbralConfianzaAuto
                              && creditos.HasValue
                              && horas.HasValue
                              && unidades.Count > 0
                              && !string.IsNullOrWhiteSpace(nombre);

        var estadoSugerido = esAutoAprobable
            ? EstadosExtraccion.Confirmada
            : EstadosExtraccion.PendienteConfirmacion;

        var resultado = new ExtraccionResultado(
            CodigoAsignaturaOrigen: codigo,
            NombreAsignaturaOrigen: nombre,
            Creditos: creditos,
            Horas: horas,
            Confianza: confianza,
            EstadoSugerido: estadoSugerido,
            EsEscaneado: false,
            RequiereConfirmacionHumana: !esAutoAprobable,
            Unidades: unidades,
            CamposFaltantes: camposFaltantes.AsReadOnly(),
            MetodoExtraccion: "ParserTextoEstructurado",
            Observacion: esAutoAprobable
                ? "Extracción completa y validada automáticamente (confianza >= 0.80)."
                : "Extracción parcial o con baja confianza (< 0.80); requiere confirmación del operador."
        );

        return Task.FromResult(resultado);
    }

    public async Task<ExtraccionResultado> ExtraerDeArchivoAsync(
        string rutaRelativaAlmacen,
        string? codigoAsignaturaFallback = null,
        CancellationToken cancellationToken = default)
    {
        var rutaCompleta = Path.GetFullPath(Path.Combine(_raizAlmacen, rutaRelativaAlmacen));
        if (!File.Exists(rutaCompleta))
        {
            throw new FileNotFoundException($"El archivo no existe en el almacenamiento: {rutaRelativaAlmacen}");
        }

        // Lectura heurística de texto plano o strings dentro de archivo
        string textoLeido;
        bool tieneTextoNativo;

        try
        {
            var bytes = await File.ReadAllBytesAsync(rutaCompleta, cancellationToken);
            var contenidoBruto = System.Text.Encoding.Latin1.GetString(bytes);
            textoLeido = contenidoBruto.StartsWith("%PDF-", StringComparison.Ordinal)
                ? ExtraerTextoPdf(contenidoBruto)
                : contenidoBruto;

            tieneTextoNativo = !string.IsNullOrWhiteSpace(textoLeido) && textoLeido.Length >= 50;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al leer contenido directo de {Ruta}; se asume escaneado.", rutaRelativaAlmacen);
            tieneTextoNativo = false;
            textoLeido = string.Empty;
        }

        return await ExtraerDeTextoAsync(textoLeido, tieneTextoNativo, codigoAsignaturaFallback, cancellationToken);
    }

    public decimal CalcularConfianza(
        int? creditos,
        int? horas,
        IReadOnlyCollection<UnidadExtraida> unidades,
        string? nombreAsignatura,
        string? codigoAsignatura)
    {
        decimal acumulado = 0.0m;

        if (!string.IsNullOrWhiteSpace(nombreAsignatura))
            acumulado += 0.20m;

        if (!string.IsNullOrWhiteSpace(codigoAsignatura))
            acumulado += 0.10m;

        if (creditos is >= 1 and <= 30)
            acumulado += 0.25m;

        if (horas is >= 1 and <= 2000)
            acumulado += 0.25m;

        if (unidades != null && unidades.Count > 0)
        {
            acumulado += 0.20m;
        }

        return Math.Min(1.00m, Math.Round(acumulado, 2));
    }

    public bool EsPdfEscaneado(string? texto, int cantidadPaginas = 1)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return true;

        var limpio = texto.Trim();
        if (limpio.Length < 30)
            return true;

        // Contar caracteres alfabéticos
        int letras = limpio.Count(char.IsLetter);
        int total = limpio.Length;

        // Si menos del 25% son letras, es ruido binario o escaneado sin OCR
        if ((double)letras / total < 0.25)
            return true;

        // Si el texto por página es excesivamente bajo (< 40 caracteres con letras)
        if (cantidadPaginas > 0 && letras / cantidadPaginas < 30)
            return true;

        return false;
    }

    #region Parsers heurísticos de texto

    private static int? ExtraerCreditos(string texto)
    {
        var match = Regex.Match(texto, @"(?i)(?:cr[eé]ditos?|cr[eé]d\.?)\s*[:=-]?\s*(\d{1,2})");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var c1) && c1 is >= 1 and <= 30)
            return c1;

        match = Regex.Match(texto, @"(?i)(\d{1,2})\s*cr[eé]ditos?");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var c2) && c2 is >= 1 and <= 30)
            return c2;

        return null;
    }

    private static int? ExtraerHoras(string texto)
    {
        var match = Regex.Match(texto, @"(?i)(?:horas?(?:\s+totales)?|carga\s+horaria|duraci[oó]n)\s*[:=-]?\s*(\d{1,4})");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var h1) && h1 is >= 1 and <= 2000)
            return h1;

        match = Regex.Match(texto, @"(?i)(\d{1,4})\s*horas?");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var h2) && h2 is >= 1 and <= 2000)
            return h2;

        return null;
    }

    private static string? ExtraerCodigoAsignatura(string texto)
    {
        var match = Regex.Match(texto, @"(?i)(?:c[oó]digo|cod\.?)\s*[:=-]?\s*([A-Z0-9\-_]{3,15})");
        if (match.Success)
            return match.Groups[1].Value.Trim().ToUpperInvariant();

        return null;
    }

    private static string? ExtraerNombreAsignatura(string texto)
    {
        var match = Regex.Match(texto, @"(?i)(?:asignatura|materia|nombre\s+de\s+la\s+materia)[^\S\r\n]*[:=-]?[^\S\r\n]*([^\r\n]{3,80})");
        if (match.Success)
        {
            var candidato = match.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(candidato))
                return candidato;
        }

        return null;
    }

    private static IReadOnlyCollection<UnidadExtraida> ExtraerUnidades(string texto)
    {
        var unidades = new List<UnidadExtraida>();
        var lineas = texto.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        var encabezado = new Regex(@"^\s*(?:unidad|cap[ií]tulo|tema)\s*(\d+|[IVXLCDM]+)[:.\-\s]+(.+)$", RegexOptions.IgnoreCase);
        var seccion = new Regex(@"^\s*\d+\.\s*[A-ZÁÉÍÓÚÑ]");

        for (var i = 0; i < lineas.Length; i++)
        {
            var match = encabezado.Match(lineas[i]);
            if (!match.Success) continue;

            var num = ConvertirNumero(match.Groups[1].Value, unidades.Count + 1);
            var titulo = match.Groups[2].Value.Trim();
            var matchHoras = Regex.Match(titulo, @"(?:\(|[-–:]\s*)(\d{1,3})\s*horas?\)?\.?$", RegexOptions.IgnoreCase);
            var horasUnidad = matchHoras.Success && int.TryParse(matchHoras.Groups[1].Value, out var h) ? h : 0;
            var nombre = matchHoras.Success ? titulo[..matchHoras.Index].Trim().TrimEnd('.', '-', '–', ':') : titulo.TrimEnd('.');

            var contenidos = new List<string>();
            for (var j = i + 1; j < lineas.Length && !encabezado.IsMatch(lineas[j]) && !seccion.IsMatch(lineas[j]); j++)
            {
                if (!string.IsNullOrWhiteSpace(lineas[j])) contenidos.Add(lineas[j].Trim());
            }

            if (!string.IsNullOrWhiteSpace(nombre) && nombre.Length >= 3)
            {
                unidades.Add(new UnidadExtraida(
                    Numero: num,
                    Nombre: nombre,
                    Horas: horasUnidad,
                    Contenidos: string.Join(" ", contenidos)
                ));
            }
        }

        // Eliminar duplicados de número
        var resultado = unidades
            .GroupBy(u => u.Numero)
            .Select(g => g.First())
            .OrderBy(u => u.Numero)
            .ToList();

        return resultado.AsReadOnly();
    }

    // ponytail: extractor propio con ZLibStream. Cubre PDFs de Word/Chrome/LibreOffice (FlateDecode + Tm).
    // Si aparece un PDF con fuentes CID o cifrado, evaluar PdfPig.
    private static string ExtraerTextoPdfSinCompresion(string contenido)
    {
        var coincidencias = Regex.Matches(contenido, @"\(((?:\\.|[^\\)])*)\)\s*Tj");
        return string.Join("\n", coincidencias.Select(m => Regex.Replace(m.Groups[1].Value, @"\\([\\()])", "$1")
            .Replace(@"\n", "\n").Replace(@"\r", "\r")));
    }

    /// <summary>
    /// Texto de PDFs con capa de texto. Primero intenta streams sin comprimir y, si no hay,
    /// descomprime los FlateDecode y reconstruye las líneas por coordenada Y del operador Tm.
    /// </summary>
    private static string ExtraerTextoPdf(string contenido)
    {
        var directo = ExtraerTextoPdfSinCompresion(contenido);
        if (!string.IsNullOrWhiteSpace(directo))
            return directo;

        var lineas = new List<string>();
        foreach (var flujo in EnumerarFlujosPdf(contenido))
        {
            var descomprimido = DescomprimirFlate(flujo);
            if (descomprimido is null || !descomprimido.Contains("BT", StringComparison.Ordinal))
                continue;

            lineas.AddRange(ExtraerLineasDeTexto(descomprimido));
        }

        return string.Join("\n", lineas);
    }

    /// <summary>Bloques crudos entre <c>stream</c> y <c>endstream</c> (sin comprimir ni interpretar).</summary>
    private static IEnumerable<byte[]> EnumerarFlujosPdf(string contenido)
    {
        var bytes = System.Text.Encoding.Latin1.GetBytes(contenido);
        var marcaInicio = System.Text.Encoding.Latin1.GetBytes("stream");
        var marcaFin = System.Text.Encoding.Latin1.GetBytes("endstream");

        var posicion = 0;
        while (posicion < bytes.Length)
        {
            var inicio = BuscarBytes(bytes, marcaInicio, posicion);
            if (inicio < 0) yield break;

            var desde = inicio + marcaInicio.Length;
            if (desde < bytes.Length && bytes[desde] == (byte)'\r') desde++;
            if (desde < bytes.Length && bytes[desde] == (byte)'\n') desde++;

            var hasta = BuscarBytes(bytes, marcaFin, desde);
            if (hasta < 0) yield break;

            var largo = hasta - desde;
            if (largo > 0)
            {
                var copia = new byte[largo];
                Array.Copy(bytes, desde, copia, 0, largo);
                yield return copia;
            }

            posicion = hasta + marcaFin.Length;
        }
    }

    private static int BuscarBytes(byte[] pajar, byte[] aguja, int desde)
    {
        for (var i = desde; i <= pajar.Length - aguja.Length; i++)
        {
            var coincide = true;
            for (var j = 0; j < aguja.Length; j++)
            {
                if (pajar[i + j] != aguja[j]) { coincide = false; break; }
            }
            if (coincide) return i;
        }
        return -1;
    }

    private static string? DescomprimirFlate(byte[] datos)
    {
        try
        {
            using var entrada = new MemoryStream(datos);
            using var zlib = new System.IO.Compression.ZLibStream(entrada, System.IO.Compression.CompressionMode.Decompress);
            using var salida = new MemoryStream();
            zlib.CopyTo(salida);
            return System.Text.Encoding.Latin1.GetString(salida.ToArray());
        }
        catch (Exception)
        {
            // No es un stream FlateDecode válido (fuente, imagen, metadatos): se ignora.
            return null;
        }
    }

    /// <summary>
    /// Reconstruye líneas visuales: agrupa los fragmentos de texto por la Y de <c>Tm</c> y los
    /// ordena por X, de modo que "Creditos:" y "4" vuelvan a quedar en la misma línea.
    /// </summary>
    private static IEnumerable<string> ExtraerLineasDeTexto(string contenido)
    {
        var fragmentos = new List<(double Y, double X, int Orden, string Texto)>();
        var orden = 0;

        foreach (Match bloque in Regex.Matches(contenido, @"BT(?<cuerpo>.*?)ET", RegexOptions.Singleline))
        {
            var cuerpo = bloque.Groups["cuerpo"].Value;
            var texto = UnirCadenasDeTexto(cuerpo);
            if (string.IsNullOrWhiteSpace(texto))
                continue;

            var posicion = Regex.Match(
                cuerpo,
                @"(-?[\d.]+)\s+(-?[\d.]+)\s+(-?[\d.]+)\s+(-?[\d.]+)\s+(-?[\d.]+)\s+(-?[\d.]+)\s+Tm");

            double x = 0;
            double y = -orden * 12d; // Sin Tm se conserva el orden de aparición.
            if (posicion.Success)
            {
                x = double.Parse(posicion.Groups[5].Value, System.Globalization.CultureInfo.InvariantCulture);
                y = double.Parse(posicion.Groups[6].Value, System.Globalization.CultureInfo.InvariantCulture);
            }

            fragmentos.Add((y, x, orden++, texto));
        }

        return fragmentos
            .GroupBy(f => Math.Round(f.Y, 1))
            .OrderByDescending(g => g.Key)
            .Select(g => string.Concat(g.OrderBy(f => f.X).ThenBy(f => f.Orden).Select(f => f.Texto)));
    }

    private static string UnirCadenasDeTexto(string cuerpo)
    {
        var texto = new System.Text.StringBuilder();
        foreach (Match cadena in Regex.Matches(cuerpo, @"\((?:\\[\s\S]|[^\\)])*\)"))
        {
            texto.Append(DesescaparCadenaPdf(cadena.Value[1..^1]));
        }
        return texto.ToString();
    }

    private static string DesescaparCadenaPdf(string valor)
    {
        var texto = new System.Text.StringBuilder(valor.Length);
        for (var i = 0; i < valor.Length; i++)
        {
            if (valor[i] != '\\' || i + 1 >= valor.Length)
            {
                texto.Append(valor[i]);
                continue;
            }

            i++;
            texto.Append(valor[i] switch
            {
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                'b' => '\b',
                'f' => '\f',
                var otro => otro
            });
        }
        return texto.ToString();
    }

    private static int ConvertirNumero(string valor, int fallback)
    {
        if (int.TryParse(valor, out var n))
            return n;

        // Números romanos básicos
        var upper = valor.ToUpperInvariant().Trim();
        return upper switch
        {
            "I" => 1,
            "II" => 2,
            "III" => 3,
            "IV" => 4,
            "V" => 5,
            "VI" => 6,
            "VII" => 7,
            "VIII" => 8,
            "IX" => 9,
            "X" => 10,
            _ => fallback
        };
    }

    #endregion
}
