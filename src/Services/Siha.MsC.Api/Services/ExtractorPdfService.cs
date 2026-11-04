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
                ? ExtraerTextoPdfSinCompresion(contenidoBruto)
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
        var match = Regex.Match(texto, @"(?i)(?:asignatura|materia|nombre\s+de\s+la\s+materia)\s*[:=-]?\s*([^\r\n]{3,80})");
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
            for (var j = i + 1; j < lineas.Length && !encabezado.IsMatch(lineas[j]); j++)
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

    // ponytail: lee literales Tj de PDFs sin comprimir (como el PDF de prueba); para flujos comprimidos se necesita PdfPig.
    private static string ExtraerTextoPdfSinCompresion(string contenido)
    {
        var coincidencias = Regex.Matches(contenido, @"\(((?:\\.|[^\\)])*)\)\s*Tj");
        return string.Join("\n", coincidencias.Select(m => Regex.Replace(m.Groups[1].Value, @"\\([\\()])", "$1")
            .Replace(@"\n", "\n").Replace(@"\r", "\r")));
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
