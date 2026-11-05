using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Siha.Contracts;
using UglyToad.PdfPig;

namespace Siha.Certificados;

/// <summary>
/// Lee y valida el certificado de homologación SIHA (formato <c>SIHA-CH-01</c>).
///
/// El formato es un texto con etiquetas fijas (<c>Etiqueta: valor</c>); varias pueden ir en la misma
/// línea y no importan tildes ni mayúsculas. Cualquier dato faltante o incoherente hace que el documento
/// se rechace con la lista completa de errores: el sistema nunca completa ni adivina datos.
///
/// Reglas del instructivo UTA de cambio de carrera que se comprueban aquí:
///  - Haber aprobado al menos el primer período académico (todas las materias de nivel 1 aprobadas).
///  - Pedir al menos una homologación, y solo con materias aprobadas (las reprobadas no se homologan).
///  - Materias vigentes: aprobadas en los últimos 10 años (el certificado ya excluye terceras matrículas).
/// </summary>
public static class LectorCertificadoSiha
{
    /// <summary>Código del formato oficial que debe aparecer en el encabezado del PDF.</summary>
    public const string CodigoFormato = "SIHA-CH-01";

    /// <summary>Nota mínima aprobatoria en la escala de 0 a 10.</summary>
    public const decimal NotaMinimaAprobatoria = 7.0m;

    /// <summary>Vigencia máxima de una materia aprobada para homologarse.</summary>
    public const int VigenciaMaximaAnos = 10;

    /// <summary>Dominio institucional obligatorio de los correos de los docentes revisores.</summary>
    public const string DominioInstitucional = "@uta.edu.ec";

    private static readonly string[] EtiquetasGenerales =
    {
        "numero de certificado", "fecha de emision", "emitido por", "estudiante", "cedula",
        "tipo de tramite", "ruta normativa", "institucion de origen", "carrera de origen", "carrera de destino",
        "docente revisor 1", "docente revisor 2"
    };

    private static readonly string[] EtiquetasMateria = { "materia", "nivel", "periodo academico", "nota", "estado" };

    private static readonly string[] EtiquetasSolicitud = { "materia aprobada", "homologar por" };

    // Las etiquetas más largas primero para que "materia aprobada" gane a "materia".
    private static readonly Regex RegexEtiqueta = new(
        @"(?<![a-z0-9])(?<clave>" + string.Join("|", EtiquetasGenerales.Concat(EtiquetasMateria).Concat(EtiquetasSolicitud)
            .Distinct().OrderByDescending(e => e.Length).Select(Regex.Escape)) + @")\s*:",
        RegexOptions.Compiled);

    private static readonly Regex RegexCorreo = new(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", RegexOptions.Compiled);

    private static readonly Regex RegexAnio = new(@"\b(19|20)\d{2}\b", RegexOptions.Compiled);

    // Pie de página ("SIHA-CH-01   Página 1 de 2"): se descarta la línea completa.
    private static readonly Regex RegexPagina = new(@"(^|\s)pagina\s+\d+\s+de\s+\d+$", RegexOptions.Compiled);

    /// <summary>Lee el certificado desde los bytes de un PDF.</summary>
    public static ResultadoLecturaCertificado Leer(byte[] pdf)
    {
        if (pdf is null || pdf.Length < 5 || Encoding.ASCII.GetString(pdf, 0, 5) != "%PDF-")
        {
            return ResultadoLecturaCertificado.Invalido(new[] { "El archivo no es un PDF." });
        }

        IReadOnlyList<string> lineas;
        try
        {
            lineas = ExtraerLineas(pdf);
        }
        catch (Exception ex)
        {
            return ResultadoLecturaCertificado.Invalido(new[] { $"No se pudo leer el PDF: {ex.Message}" });
        }

        if (lineas.Count == 0)
        {
            return ResultadoLecturaCertificado.Invalido(new[]
            {
                "El PDF no tiene texto seleccionable (parece escaneado). El certificado SIHA debe generarse digitalmente con la plantilla oficial."
            });
        }

        return LeerTexto(lineas);
    }

    /// <summary>Extrae el texto del PDF como líneas, en orden de lectura (arriba-abajo, izquierda-derecha).</summary>
    public static IReadOnlyList<string> ExtraerLineas(byte[] pdf)
    {
        var lineas = new List<string>();
        using var documento = PdfDocument.Open(pdf);
        foreach (var pagina in documento.GetPages())
        {
            var palabras = pagina.GetWords()
                .Where(p => !string.IsNullOrWhiteSpace(p.Text))
                .OrderByDescending(p => p.BoundingBox.Bottom)
                .ThenBy(p => p.BoundingBox.Left)
                .ToList();

            var actual = new List<UglyToad.PdfPig.Content.Word>();
            double? baseActual = null;
            foreach (var palabra in palabras)
            {
                var baseLinea = palabra.BoundingBox.Bottom;
                if (baseActual is not null && Math.Abs(baseActual.Value - baseLinea) > 3.0)
                {
                    lineas.Add(UnirLinea(actual));
                    actual.Clear();
                }

                if (actual.Count == 0) baseActual = baseLinea;
                actual.Add(palabra);
            }

            if (actual.Count > 0) lineas.Add(UnirLinea(actual));
        }

        return lineas.Where(l => l.Length > 0).ToList();
    }

    private static string UnirLinea(IEnumerable<UglyToad.PdfPig.Content.Word> palabras)
        => string.Join(" ", palabras.OrderBy(p => p.BoundingBox.Left).Select(p => p.Text)).Trim();

    /// <summary>Interpreta y valida el texto del certificado (una entrada por línea).</summary>
    public static ResultadoLecturaCertificado LeerTexto(IEnumerable<string> lineasTexto)
    {
        var lineas = lineasTexto
            .Select(l => Regex.Replace(l ?? string.Empty, @"\s+", " ").Trim())
            .Where(l => l.Length > 0)
            .ToList();

        var textoCompleto = Normalizar(string.Join("\n", lineas));
        if (!textoCompleto.Contains("certificado de homologacion") ||
            !textoCompleto.Contains(CodigoFormato.ToLowerInvariant()))
        {
            return ResultadoLecturaCertificado.Invalido(new[]
            {
                $"El documento no corresponde a la plantilla oficial SIHA ({CodigoFormato}). Solo se acepta el certificado de homologación preaprobado."
            });
        }

        var generales = new Dictionary<string, string>();
        var materias = new List<Dictionary<string, string>>();
        var solicitudes = new List<(List<string> Origen, string? Destino)>();
        Dictionary<string, string>? materia = null;

        foreach (var linea in lineas)
        {
            var normal = Normalizar(linea);
            // Lo que sigue a la marca de cierre (firmas, sellos) no forma parte de los datos.
            if (normal.StartsWith("fin del certificado")) break;
            if (RegexPagina.IsMatch(normal)) continue;

            foreach (var (clave, valor) in Pares(linea, normal))
            {
                switch (clave)
                {
                    case "materia":
                        materia = new Dictionary<string, string> { ["materia"] = valor };
                        materias.Add(materia);
                        break;
                    case "materia aprobada":
                        // Varias "Materia aprobada" seguidas se suman a la misma solicitud (varias para una).
                        if (solicitudes.Count == 0 || solicitudes[^1].Destino is not null)
                            solicitudes.Add((new List<string>(), null));
                        solicitudes[^1].Origen.Add(valor);
                        break;
                    case "homologar por":
                        if (solicitudes.Count == 0 || solicitudes[^1].Destino is not null)
                            solicitudes.Add((new List<string>(), valor));
                        else
                            solicitudes[^1] = (solicitudes[^1].Origen, valor);
                        break;
                    default:
                        if (EtiquetasMateria.Contains(clave) && materia is not null)
                            materia[clave] = valor;
                        else if (EtiquetasGenerales.Contains(clave))
                            generales[clave] = valor;
                        break;
                }
            }
        }

        return Validar(generales, materias, solicitudes);
    }

    private static ResultadoLecturaCertificado Validar(
        Dictionary<string, string> g,
        List<Dictionary<string, string>> leidas,
        List<(List<string> Origen, string? Destino)> pedidas)
    {
        var errores = new List<string>();

        string Requerido(string clave, string etiqueta)
        {
            if (g.TryGetValue(clave, out var valor) && !string.IsNullOrWhiteSpace(valor)) return valor.Trim();
            errores.Add($"Falta el dato '{etiqueta}'.");
            return string.Empty;
        }

        var numero = Requerido("numero de certificado", "Número de certificado");
        var fechaEmisionTexto = Requerido("fecha de emision", "Fecha de emisión");
        var emitidoPor = Requerido("emitido por", "Emitido por");
        var estudiante = Requerido("estudiante", "Estudiante");
        var cedula = Requerido("cedula", "Cédula");
        var tipoTexto = Requerido("tipo de tramite", "Tipo de trámite");
        var rutaTexto = Requerido("ruta normativa", "Ruta normativa");
        var institucion = Requerido("institucion de origen", "Institución de origen");
        var carreraOrigen = Requerido("carrera de origen", "Carrera de origen");
        var carreraDestino = Requerido("carrera de destino", "Carrera de destino");

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var fechaEmision = default(DateOnly);
        if (fechaEmisionTexto.Length > 0 && !IntentarFecha(fechaEmisionTexto, out fechaEmision))
            errores.Add($"La fecha de emisión '{fechaEmisionTexto}' no es válida (use AAAA-MM-DD o DD/MM/AAAA).");
        else if (fechaEmisionTexto.Length > 0 && fechaEmision > hoy.AddDays(1))
            errores.Add("La fecha de emisión está en el futuro.");

        if (cedula.Length > 0 && !Regex.IsMatch(cedula, @"^\d{10}$"))
            errores.Add($"La cédula '{cedula}' debe tener 10 dígitos.");

        var tipoNormal = Normalizar(tipoTexto);
        var tipo = tipoNormal.Contains("interno") || tipoNormal.Contains("cambio de carrera") ? TiposTramite.Interno
            : tipoNormal.Contains("externo") || tipoNormal.Contains("movilidad") ? TiposTramite.Externo
            : string.Empty;
        if (tipoTexto.Length > 0 && tipo.Length == 0)
            errores.Add($"El tipo de trámite '{tipoTexto}' no es válido (Cambio de carrera o Movilidad externa).");

        var ruta = RutasNormativas.Todas.FirstOrDefault(r => rutaTexto.ToUpperInvariant().Contains(r)) ?? string.Empty;
        if (rutaTexto.Length > 0 && ruta.Length == 0)
            errores.Add($"La ruta normativa '{rutaTexto}' no es válida ({string.Join(" o ", RutasNormativas.Todas)}).");
        if (tipo == TiposTramite.Interno && ruta.Length > 0 && ruta != RutasNormativas.CambioCarreraOu)
            errores.Add($"Un cambio de carrera debe usar la ruta {RutasNormativas.CambioCarreraOu}.");
        if (tipo == TiposTramite.Externo && ruta.Length > 0 && ruta != RutasNormativas.Movilidad)
            errores.Add($"Una movilidad externa debe usar la ruta {RutasNormativas.Movilidad}.");

        if (carreraOrigen.Length > 0 && Normalizar(carreraOrigen) == Normalizar(carreraDestino) && tipo == TiposTramite.Interno)
            errores.Add("En un cambio de carrera la carrera de origen y la de destino deben ser distintas.");
        foreach (var (texto, etiqueta) in new[] { (carreraOrigen, "Carrera de origen"), (carreraDestino, "Carrera de destino") })
        {
            if (Regex.IsMatch(texto, @"^[A-Z]{2,5}(\s*-|$)"))
                errores.Add($"'{etiqueta}' debe escribirse con el nombre completo de la carrera, sin siglas (se leyó '{texto}').");
        }

        // ---- Docentes revisores ----
        var revisores = new List<RevisorCertificado>();
        foreach (var n in new[] { 1, 2 })
        {
            var texto = Requerido($"docente revisor {n}", $"Docente revisor {n}");
            if (texto.Length == 0) continue;
            var correo = RegexCorreo.Match(texto);
            if (!correo.Success)
            {
                errores.Add($"'Docente revisor {n}' debe tener el formato 'Nombre - correo{DominioInstitucional}' (se leyó '{texto}').");
                continue;
            }

            var direccion = correo.Value.ToLowerInvariant();
            if (!direccion.EndsWith(DominioInstitucional, StringComparison.Ordinal))
                errores.Add($"El correo del docente revisor {n} ({direccion}) debe ser institucional ({DominioInstitucional}).");
            var nombreRevisor = texto.Remove(correo.Index, correo.Length).Trim(' ', '-', '–', '—', '<', '>', '(', ')', ',');
            revisores.Add(new RevisorCertificado(nombreRevisor.Length > 0 ? nombreRevisor : direccion, direccion));
        }

        if (revisores.Count == 2 && revisores[0].Correo == revisores[1].Correo)
            errores.Add("Los dos docentes revisores deben ser personas distintas.");

        // ---- Historial académico ----
        if (leidas.Count == 0)
            errores.Add("El certificado no tiene historial académico (líneas 'Materia: Nombre completo').");

        var materias = new List<MateriaCursada>();
        var anioEmision = fechaEmision != default ? fechaEmision.Year : hoy.Year;
        foreach (var m in leidas)
        {
            var nombre = m["materia"].Trim();
            var prefijo = $"Materia '{nombre}'";

            var nivel = 0;
            if (!m.TryGetValue("nivel", out var nivelTexto) || !int.TryParse(Regex.Match(nivelTexto, @"\d+").Value, out nivel) || nivel is < 1 or > 10)
                errores.Add($"{prefijo}: falta el 'Nivel' (1 a 10).");

            m.TryGetValue("periodo academico", out var periodo);
            periodo = periodo?.Trim() ?? string.Empty;
            var anio = RegexAnio.Matches(periodo).Select(x => int.Parse(x.Value, CultureInfo.InvariantCulture)).DefaultIfEmpty(0).Max();
            if (periodo.Length == 0 || anio == 0)
                errores.Add($"{prefijo}: falta el 'Período académico' (por ejemplo 'Abr-Sep 2025').");

            var nota = -1m;
            if (!m.TryGetValue("nota", out var notaTexto) ||
                !decimal.TryParse(notaTexto.Replace(',', '.').Split(' ')[0], NumberStyles.Number, CultureInfo.InvariantCulture, out nota) ||
                nota is < 0 or > 10)
                errores.Add($"{prefijo}: falta la 'Nota' (escala de 0 a 10).");

            var estado = Normalizar(m.GetValueOrDefault("estado") ?? string.Empty);
            var aprobada = estado.StartsWith("aprob");
            if (!aprobada && !estado.StartsWith("reprob"))
                errores.Add($"{prefijo}: el 'Estado' debe ser Aprobada o Reprobada.");
            else if (aprobada && nota is >= 0 and < NotaMinimaAprobatoria)
                errores.Add($"{prefijo}: figura como Aprobada pero su nota {Formato(nota)} es menor que {Formato(NotaMinimaAprobatoria)}.");
            else if (!aprobada && nota >= NotaMinimaAprobatoria)
                errores.Add($"{prefijo}: figura como Reprobada pero su nota {Formato(nota)} es aprobatoria.");

            if (aprobada && anio > 0 && anio < anioEmision - VigenciaMaximaAnos)
                errores.Add($"{prefijo}: se aprobó en {periodo}, hace más de {VigenciaMaximaAnos} años; el certificado solo debe incluir materias vigentes.");

            materias.Add(new MateriaCursada(nombre, nivel, periodo, Math.Max(nota, 0), aprobada));
        }

        foreach (var repetida in materias.GroupBy(x => Normalizar(x.Nombre)).Where(gr => gr.Count() > 1))
            errores.Add($"La materia '{repetida.First().Nombre}' aparece más de una vez en el historial.");

        // Regla UTA: haber aprobado al menos el primer período académico.
        var primerNivel = materias.Where(x => x.Nivel == 1).ToList();
        if (materias.Count > 0 && primerNivel.Count == 0)
            errores.Add("El historial no incluye materias del primer nivel: debe constar que aprobó al menos el primer período académico.");
        else if (primerNivel.Any(x => !x.Aprobada))
            errores.Add("No aprobó todo el primer período académico (" +
                        string.Join(", ", primerNivel.Where(x => !x.Aprobada).Select(x => x.Nombre)) +
                        "); el instructivo exige haberlo aprobado para solicitar el cambio.");

        // ---- Materias que solicita homologar ----
        var solicitudes = new List<SolicitudHomologacion>();
        if (pedidas.Count == 0)
            errores.Add("El certificado no indica qué materia quiere homologar ('Materia aprobada: ...' y 'Homologar por: ...').");

        var usadas = new HashSet<string>();
        for (var i = 0; i < pedidas.Count; i++)
        {
            var (origen, destino) = pedidas[i];
            var prefijo = $"Solicitud {i + 1}";
            if (string.IsNullOrWhiteSpace(destino))
                errores.Add($"{prefijo}: falta 'Homologar por' (la materia de la carrera de destino).");
            if (origen.Count == 0)
                errores.Add($"{prefijo}: falta 'Materia aprobada'.");

            foreach (var o in origen)
            {
                var cursada = materias.FirstOrDefault(x => Normalizar(x.Nombre) == Normalizar(o));
                if (cursada is null)
                    errores.Add($"{prefijo}: '{o}' no está en el historial académico.");
                else if (!cursada.Aprobada)
                    errores.Add($"{prefijo}: '{o}' está reprobada; solo se pueden homologar materias aprobadas.");
                if (!usadas.Add(Normalizar(o)))
                    errores.Add($"{prefijo}: '{o}' ya se usa en otra solicitud; una materia aprobada sirve para una sola homologación.");
            }

            solicitudes.Add(new SolicitudHomologacion(i + 1, origen.Select(o => o.Trim()).ToList(), destino?.Trim() ?? string.Empty));
        }

        foreach (var repetido in solicitudes.Where(s => s.MateriaDestino.Length > 0).GroupBy(s => Normalizar(s.MateriaDestino)).Where(gr => gr.Count() > 1))
            errores.Add($"La materia de destino '{repetido.First().MateriaDestino}' se pide más de una vez.");

        if (errores.Count > 0) return ResultadoLecturaCertificado.Invalido(errores.Distinct());

        return ResultadoLecturaCertificado.Valido(new CertificadoSiha(
            numero, fechaEmision, emitidoPor, estudiante, cedula, tipo, ruta, institucion,
            carreraOrigen, carreraDestino, materias, solicitudes, revisores));
    }

    /// <summary>Nota con dos decimales y coma, igual que en el resto de la aplicación ("6,50").</summary>
    private static string Formato(decimal n) => n.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');

    private static List<(string Clave, string Valor)> Pares(string original, string normal)
    {
        var resultado = new List<(string, string)>();
        var coincidencias = RegexEtiqueta.Matches(normal);
        if (coincidencias.Count == 0 || coincidencias[0].Index != 0) return resultado;

        for (var i = 0; i < coincidencias.Count; i++)
        {
            var inicio = coincidencias[i].Index + coincidencias[i].Length;
            var fin = i + 1 < coincidencias.Count ? coincidencias[i + 1].Index : original.Length;
            resultado.Add((coincidencias[i].Groups["clave"].Value, original[inicio..fin].Trim()));
        }

        return resultado;
    }

    private static bool IntentarFecha(string texto, out DateOnly fecha)
    {
        var limpio = texto.Trim().Split(' ')[0];
        return DateOnly.TryParseExact(limpio, new[] { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy" },
            CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha);
    }

    /// <summary>
    /// Minúsculas y sin tildes, conservando la longitud del texto (un carácter por carácter) para
    /// poder recortar los valores desde la línea original.
    /// </summary>
    public static string Normalizar(string texto)
    {
        var sb = new StringBuilder(texto.Length);
        foreach (var c in texto)
        {
            var descompuesto = c.ToString().Normalize(NormalizationForm.FormD);
            sb.Append(char.ToLowerInvariant(descompuesto[0]));
        }

        return sb.ToString();
    }
}
