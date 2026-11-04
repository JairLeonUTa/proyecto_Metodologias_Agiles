using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Siha.Contracts;
using UglyToad.PdfPig;

namespace Siha.Certificados;

/// <summary>
/// Lee y valida el certificado de homologación SIHA (formato <c>SIHA-CH-01</c>).
///
/// El formato es un texto con etiquetas fijas (<c>Etiqueta: valor</c>). Varias etiquetas pueden ir en
/// la misma línea (por ejemplo <c>Semestre: 1   Créditos: 4   Horas totales: 64</c>). Las tildes y las
/// mayúsculas no importan. Cualquier dato faltante o incoherente hace que el documento se rechace con
/// la lista completa de errores: el sistema nunca completa ni adivina datos.
/// </summary>
public static class LectorCertificadoSiha
{
    /// <summary>Código del formato oficial que debe aparecer en el encabezado del PDF.</summary>
    public const string CodigoFormato = "SIHA-CH-01";

    /// <summary>Nota mínima aprobatoria en la escala de 0 a 10 que usa el certificado.</summary>
    public const decimal NotaMinimaAprobatoria = 7.0m;

    /// <summary>
    /// Vigencia máxima de una materia aprobada. El certificado llega ya filtrado por Secretaría:
    /// solo trae materias de hasta 10 años y sin tercera matrícula, así que el sistema ya no las controla.
    /// </summary>
    public const int VigenciaMaximaAnos = 10;

    private static readonly string[] EtiquetasGenerales =
    {
        "numero de certificado", "fecha de emision", "emitido por", "estudiante", "cedula",
        "tipo de tramite", "ruta normativa", "institucion de origen", "carrera de origen", "carrera de destino",
        "docente revisor 1", "docente revisor 2"
    };

    /// <summary>Dominio institucional obligatorio de los correos de los docentes revisores.</summary>
    public const string DominioInstitucional = "@uta.edu.ec";

    private static readonly Regex RegexCorreo = new(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", RegexOptions.Compiled);

    private static readonly string[] EtiquetasAsignatura =
    {
        "asignatura", "semestre", "creditos", "horas totales", "fecha de aprobacion", "nota",
        "numero de matricula", "matricula", "contenidos"
    };

    // Las etiquetas más largas primero para que "numero de matricula" gane a "matricula".
    private static readonly Regex RegexEtiqueta = new(
        @"(?<![a-z0-9])(?<clave>" + string.Join("|", EtiquetasGenerales.Concat(EtiquetasAsignatura)
            .Distinct().OrderByDescending(e => e.Length).Select(Regex.Escape)) + @")\s*:",
        RegexOptions.Compiled);

    private static readonly Regex RegexUnidad = new(
        @"^unidad\s+(?<num>\d+)\s*[:.\-–]\s*(?<nombre>.+?)\s*\(\s*(?<horas>\d+)\s*(h|horas?)\s*\)\s*$",
        RegexOptions.Compiled);

    // "CÓDIGO - Nombre": el guion separador va entre espacios, así los códigos pueden llevar guiones (UC-ED).
    private static readonly Regex RegexCodigoNombre = new(
        @"^(?<codigo>[A-Za-z0-9][A-Za-z0-9._/\-]*?)\s+[-–—]\s+(?<nombre>.+)$", RegexOptions.Compiled);

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
        var asignaturas = new List<AsignaturaEnLectura>();
        AsignaturaEnLectura? asignatura = null;
        UnidadEnLectura? unidad = null;
        var leyendoContenidos = false;

        foreach (var linea in lineas)
        {
            var normal = Normalizar(linea);
            // Todo lo que sigue a la marca de cierre (firmas, sellos) no forma parte de los datos.
            if (normal.StartsWith("fin del certificado")) break;
            if (RegexPagina.IsMatch(normal)) continue;

            var unidadMatch = RegexUnidad.Match(normal);
            if (unidadMatch.Success && asignatura is not null)
            {
                var inicioNombre = unidadMatch.Groups["nombre"].Index;
                unidad = new UnidadEnLectura
                {
                    Numero = int.Parse(unidadMatch.Groups["num"].Value, CultureInfo.InvariantCulture),
                    Nombre = linea.Substring(inicioNombre, unidadMatch.Groups["nombre"].Length).Trim(),
                    Horas = int.Parse(unidadMatch.Groups["horas"].Value, CultureInfo.InvariantCulture)
                };
                asignatura.Unidades.Add(unidad);
                leyendoContenidos = false;
                continue;
            }

            var pares = Pares(linea, normal);
            if (pares.Count == 0)
            {
                // Línea sin etiqueta: continuación de los contenidos de la unidad en curso
                // (el PDF corta los textos largos en varias líneas). Encabezados se ignoran.
                if (leyendoContenidos && unidad is not null)
                {
                    unidad.Contenidos.Append(' ').Append(linea);
                }

                continue;
            }

            foreach (var (clave, valor) in pares)
            {
                leyendoContenidos = false;
                switch (clave)
                {
                    case "asignatura":
                        asignatura = new AsignaturaEnLectura { Encabezado = valor };
                        asignaturas.Add(asignatura);
                        unidad = null;
                        break;
                    case "contenidos":
                        if (unidad is null)
                        {
                            asignatura?.Errores.Add("Hay contenidos sin una línea 'Unidad N: nombre (H horas)' antes.");
                            break;
                        }

                        unidad.Contenidos.Append(valor);
                        leyendoContenidos = true;
                        break;
                    default:
                        if (asignatura is not null && EtiquetasAsignatura.Contains(clave))
                        {
                            asignatura.Campos[clave == "numero de matricula" ? "matricula" : clave] = valor;
                        }
                        else if (EtiquetasGenerales.Contains(clave))
                        {
                            generales[clave] = valor;
                        }

                        break;
                }
            }
        }

        return Validar(generales, asignaturas);
    }

    private static ResultadoLecturaCertificado Validar(
        Dictionary<string, string> g, List<AsignaturaEnLectura> leidas)
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
        var carreraOrigenTexto = Requerido("carrera de origen", "Carrera de origen");
        var carreraDestinoTexto = Requerido("carrera de destino", "Carrera de destino");

        var fechaEmision = default(DateOnly);
        if (fechaEmisionTexto.Length > 0 && !IntentarFecha(fechaEmisionTexto, out fechaEmision))
            errores.Add($"La fecha de emisión '{fechaEmisionTexto}' no es válida (use AAAA-MM-DD o DD/MM/AAAA).");
        else if (fechaEmisionTexto.Length > 0 && fechaEmision > DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1))
            errores.Add("La fecha de emisión está en el futuro.");

        if (cedula.Length > 0 && !Regex.IsMatch(cedula, @"^\d{10}$"))
            errores.Add($"La cédula '{cedula}' debe tener 10 dígitos.");

        var tipo = Normalizar(tipoTexto).ToUpperInvariant();
        if (tipoTexto.Length > 0 && !TiposTramite.Todos.Contains(tipo))
            errores.Add($"El tipo de trámite '{tipoTexto}' no es válido (INTERNO o EXTERNO).");

        var ruta = RutasNormativas.Todas.FirstOrDefault(r => rutaTexto.ToUpperInvariant().Contains(r)) ?? string.Empty;
        if (rutaTexto.Length > 0 && ruta.Length == 0)
            errores.Add($"La ruta normativa '{rutaTexto}' no es válida ({string.Join(" o ", RutasNormativas.Todas)}).");

        if (tipo == TiposTramite.Interno && ruta.Length > 0 && ruta != RutasNormativas.CambioCarreraOu)
            errores.Add($"Un trámite INTERNO (cambio de carrera) debe usar la ruta {RutasNormativas.CambioCarreraOu}.");
        if (tipo == TiposTramite.Externo && ruta.Length > 0 && ruta != RutasNormativas.Movilidad)
            errores.Add($"Un trámite EXTERNO (otra institución) debe usar la ruta {RutasNormativas.Movilidad}.");

        var (codOrigen, nomOrigen) = CodigoYNombre(carreraOrigenTexto, "Carrera de origen", errores);
        var (codDestino, nomDestino) = CodigoYNombre(carreraDestinoTexto, "Carrera de destino", errores);
        if (codOrigen.Length > 0 && codOrigen == codDestino && tipo == TiposTramite.Interno)
            errores.Add("En un cambio de carrera la carrera de origen y la de destino deben ser distintas.");

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

        if (leidas.Count == 0)
            errores.Add("El certificado no lista ninguna asignatura aprobada (líneas 'Asignatura: CÓDIGO - Nombre').");

        var asignaturas = new List<AsignaturaCertificada>();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var a in leidas)
        {
            var (codigo, nombre) = CodigoYNombre(a.Encabezado, "Asignatura", errores);
            var prefijo = $"Asignatura {(codigo.Length > 0 ? codigo : a.Encabezado)}";
            errores.AddRange(a.Errores.Select(e => $"{prefijo}: {e}"));

            int Entero(string clave, string etiqueta, int minimo, int maximo)
            {
                if (!a.Campos.TryGetValue(clave, out var texto) || string.IsNullOrWhiteSpace(texto))
                {
                    errores.Add($"{prefijo}: falta '{etiqueta}'.");
                    return 0;
                }

                var limpio = Regex.Match(texto, @"\d+").Value;
                if (!int.TryParse(limpio, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n < minimo || n > maximo)
                {
                    errores.Add($"{prefijo}: '{etiqueta}' = '{texto}' no es válido ({minimo} a {maximo}).");
                    return 0;
                }

                return n;
            }

            var semestre = Entero("semestre", "Semestre", 1, 12);
            var creditos = Entero("creditos", "Créditos", 1, 30);
            var horas = Entero("horas totales", "Horas totales", 1, 2000);
            // La matrícula ya no forma parte del formato (el certificado excluye las terceras matrículas).

            var fecha = default(DateOnly);
            if (!a.Campos.TryGetValue("fecha de aprobacion", out var fechaTexto) || string.IsNullOrWhiteSpace(fechaTexto))
                errores.Add($"{prefijo}: falta 'Fecha de aprobación'.");
            else if (!IntentarFecha(fechaTexto, out fecha))
                errores.Add($"{prefijo}: la fecha de aprobación '{fechaTexto}' no es válida.");
            else if (fecha > hoy || (fechaEmision != default && fecha > fechaEmision))
                errores.Add($"{prefijo}: la fecha de aprobación {fecha:yyyy-MM-dd} es posterior a la emisión del certificado.");
            else if (fecha < (fechaEmision != default ? fechaEmision : hoy).AddYears(-VigenciaMaximaAnos))
                errores.Add($"{prefijo}: fue aprobada el {fecha:yyyy-MM-dd}, hace más de {VigenciaMaximaAnos} años; el certificado solo debe incluir materias vigentes.");

            var nota = 0m;
            if (!a.Campos.TryGetValue("nota", out var notaTexto) || string.IsNullOrWhiteSpace(notaTexto))
                errores.Add($"{prefijo}: falta 'Nota'.");
            else if (!decimal.TryParse(notaTexto.Replace(',', '.').Split(' ')[0], NumberStyles.Number, CultureInfo.InvariantCulture, out nota) || nota < 0 || nota > 10)
                errores.Add($"{prefijo}: la nota '{notaTexto}' no es válida (escala de 0 a 10).");
            else if (nota < NotaMinimaAprobatoria)
                errores.Add($"{prefijo}: la nota {nota.ToString("0.##", CultureInfo.InvariantCulture)} no es aprobatoria (mínimo {NotaMinimaAprobatoria.ToString("0", CultureInfo.InvariantCulture)}).");

            if (a.Unidades.Count == 0)
                errores.Add($"{prefijo}: no tiene unidades temáticas ('Unidad N: nombre (H horas)' y 'Contenidos: ...').");

            foreach (var u in a.Unidades.Where(u => u.Contenidos.ToString().Trim().Length == 0))
                errores.Add($"{prefijo}: la unidad {u.Numero} ({u.Nombre}) no tiene contenidos.");

            if (a.Unidades.GroupBy(u => u.Numero).Any(grupo => grupo.Count() > 1))
                errores.Add($"{prefijo}: hay unidades con el mismo número.");

            var sumaHoras = a.Unidades.Sum(u => u.Horas);
            if (horas > 0 && a.Unidades.Count > 0 && sumaHoras != horas)
                errores.Add($"{prefijo}: las horas de las unidades suman {sumaHoras} y no coinciden con las horas totales ({horas}).");

            asignaturas.Add(new AsignaturaCertificada(
                codigo.ToUpperInvariant(), nombre, semestre, creditos, horas, fecha, nota,
                a.Unidades.Select(u => new UnidadCertificada(
                    u.Numero, u.Nombre, u.Horas, Regex.Replace(u.Contenidos.ToString(), @"\s+", " ").Trim())).ToList()));
        }

        foreach (var repetida in asignaturas.Where(a => a.Codigo.Length > 0).GroupBy(a => a.Codigo).Where(gr => gr.Count() > 1))
            errores.Add($"La asignatura {repetida.Key} aparece más de una vez.");

        if (errores.Count > 0) return ResultadoLecturaCertificado.Invalido(errores.Distinct());

        return ResultadoLecturaCertificado.Valido(new CertificadoSiha(
            numero, fechaEmision, emitidoPor, estudiante, cedula, tipo, ruta, institucion,
            codOrigen.ToUpperInvariant(), nomOrigen, codDestino.ToUpperInvariant(), nomDestino, asignaturas, revisores));
    }

    private static (string Codigo, string Nombre) CodigoYNombre(string texto, string etiqueta, List<string> errores)
    {
        if (string.IsNullOrWhiteSpace(texto)) return (string.Empty, string.Empty);
        var m = RegexCodigoNombre.Match(texto.Trim());
        if (m.Success) return (m.Groups["codigo"].Value.Trim(), m.Groups["nombre"].Value.Trim());
        errores.Add($"'{etiqueta}' debe tener el formato 'CÓDIGO - Nombre' (se leyó '{texto}').");
        return (string.Empty, string.Empty);
    }

    private static List<(string Clave, string Valor)> Pares(string original, string normal)
    {
        var resultado = new List<(string, string)>();
        var coincidencias = RegexEtiqueta.Matches(normal);
        if (coincidencias.Count == 0 || coincidencias[0].Index != 0) return resultado;

        // "Contenidos:" ocupa el resto de la línea aunque el texto contenga otra etiqueta.
        if (coincidencias[0].Groups["clave"].Value == "contenidos")
        {
            resultado.Add(("contenidos", original[coincidencias[0].Length..].Trim()));
            return resultado;
        }

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

    private sealed class AsignaturaEnLectura
    {
        public string Encabezado { get; init; } = string.Empty;
        public Dictionary<string, string> Campos { get; } = new();
        public List<UnidadEnLectura> Unidades { get; } = new();
        public List<string> Errores { get; } = new();
    }

    private sealed class UnidadEnLectura
    {
        public int Numero { get; init; }
        public string Nombre { get; init; } = string.Empty;
        public int Horas { get; init; }
        public StringBuilder Contenidos { get; } = new();
    }
}
