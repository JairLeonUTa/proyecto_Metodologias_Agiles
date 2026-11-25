using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Siha.Certificados;

/// <summary>Materia de la malla vigente con lo que se necesita para compararla: nombre, nivel, créditos y temas.</summary>
public sealed record MateriaCatalogo(string Nombre, int Nivel, int Creditos, string Contenidos);

/// <summary>
/// Qué tan relacionadas están las materias aprobadas con la materia que se pide homologar.
/// <see cref="Puntaje"/> va de 0 a 1 (parecido de los temas); <see cref="NombreEnComun"/> indica que los
/// nombres comparten una palabra propia de la materia ("Programación", "Cálculo", "Física").
/// </summary>
public sealed record Afinidad(double Puntaje, bool NombreEnComun, bool SinDatos)
{
    /// <summary>
    /// Se relacionan si comparten una palabra propia del nombre o sus temas se parecen al menos
    /// <see cref="AfinidadMaterias.Minimo"/>. Si no hay temas para comparar (por ejemplo, el catálogo
    /// no respondió o la materia no es de la malla) no se bloquea: lo deciden el agente y los docentes.
    /// </summary>
    public bool SeRelacionan => SinDatos || NombreEnComun || Puntaje >= AfinidadMaterias.Minimo;

    public int Porcentaje => (int)Math.Round(Puntaje * 100);
}

/// <summary>Materia de destino sugerida para una materia aprobada.</summary>
public sealed record SugerenciaHomologacion(string MateriaOrigen, string MateriaDestino, int Porcentaje);

/// <summary>
/// Comprobación rápida, antes de enviar la solicitud al agente, de que lo que se pide homologar tiene
/// sentido: que la materia aprobada y la de la carrera nueva traten temas parecidos (no "Química" por
/// "Algoritmos"). Compara las palabras del nombre y de las unidades de cada sílabo (TF-IDF con
/// similitud coseno). No reemplaza el análisis del agente: solo evita pedir cualquier cosa.
/// </summary>
public sealed class AfinidadMaterias
{
    /// <summary>Por debajo de este parecido de temas (y sin palabras del nombre en común) no se relacionan.</summary>
    public const double Minimo = 0.10;

    /// <summary>Parecido mínimo para sugerir automáticamente la materia de destino.</summary>
    public const double MinimoSugerencia = 0.18;

    /// <summary>Palabras que no dicen de qué trata la materia (se ignoran al comparar).</summary>
    private static readonly HashSet<string> Vacias = new(StringComparer.Ordinal)
    {
        "para", "como", "entre", "sobre", "desde", "hasta", "segun", "mediante", "cada", "esta", "este", "estos",
        "estas", "otros", "otras", "tipo", "tipos", "usos", "aplicacion", "aplicaciones", "introduccion",
        "fundamentos", "conceptos", "basicos", "basicas", "basica", "basico", "general", "generales", "problemas",
        "resolucion", "estudio", "analisis", "metodos", "metodo", "principios", "propiedades", "unidad", "unidades",
        "tema", "temas", "caso", "casos", "parte", "partes", "forma", "formas", "nivel", "area", "areas", "tecnicas",
        "tecnica", "herramientas", "sistema", "sistemas", "proceso", "procesos", "modelo", "modelos", "diseno",
        "practica", "practicas", "teoria", "teorica", "aplicada", "aplicadas", "aplicar", "desarrollo", "definicion",
        "definiciones", "ejemplos", "ejercicios"
    };

    /// <summary>Palabras del nombre demasiado generales para decir que dos materias son de lo mismo.</summary>
    private static readonly HashSet<string> NombresGenerales = new(StringComparer.Ordinal)
    {
        "avanza", "ingeni", "tecnol", "gestio", "inform", "comuni", "labora", "realid", "nacion", "metodo", "invest"
    };

    private readonly Dictionary<string, Dictionary<string, int>> _origen;
    private readonly Dictionary<string, Dictionary<string, int>> _destino;
    private readonly Dictionary<string, string> _nombresDestino;
    private readonly Dictionary<string, double> _idf = new(StringComparer.Ordinal);

    /// <param name="origen">Malla de la carrera de origen (vacía en movilidad externa).</param>
    /// <param name="destino">Malla de la carrera de destino.</param>
    public AfinidadMaterias(IEnumerable<MateriaCatalogo> origen, IEnumerable<MateriaCatalogo> destino)
    {
        _origen = Indexar(origen, out _);
        _destino = Indexar(destino, out _nombresDestino);

        var documentos = _origen.Values.Concat(_destino.Values).ToList();
        var frecuencia = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var palabra in documentos.SelectMany(d => d.Keys))
            frecuencia[palabra] = frecuencia.GetValueOrDefault(palabra) + 1;
        foreach (var (palabra, veces) in frecuencia)
            _idf[palabra] = Math.Log(1 + (double)documentos.Count / veces);
    }

    /// <summary>True si se conoce la malla de destino (si no, no se puede comparar nada).</summary>
    public bool HayDatos => _destino.Count > 0;

    /// <summary>Afinidad de una o varias materias aprobadas con una materia de destino.</summary>
    public Afinidad Calcular(IEnumerable<string> materiasOrigen, string materiaDestino)
    {
        var claveDestino = NombresMaterias.Clave(materiaDestino);
        if (!_destino.TryGetValue(claveDestino, out var destino))
        {
            var oficial = _nombresDestino.Keys.FirstOrDefault(k => NombresMaterias.Coinciden(k, materiaDestino));
            if (oficial is null) return new Afinidad(0, false, SinDatos: true);
            destino = _destino[oficial];
            claveDestino = oficial;
        }

        var nombres = materiasOrigen.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        if (nombres.Count == 0) return new Afinidad(0, false, SinDatos: true);

        // Las materias aprobadas que están en la malla de origen aportan sus temas; las demás (movilidad
        // externa o un nombre distinto) solo su nombre.
        var texto = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var nombre in nombres)
        {
            var clave = NombresMaterias.Clave(nombre);
            var materia = _origen.TryGetValue(clave, out var m) ? m
                : _origen.FirstOrDefault(o => NombresMaterias.Coinciden(o.Key, nombre)).Value
                  ?? Contar(Palabras(nombre), peso: 3);
            foreach (var (palabra, veces) in materia) texto[palabra] = texto.GetValueOrDefault(palabra) + veces;
        }

        var palabrasNombreDestino = PalabrasPropias(claveDestino);
        var nombreEnComun = nombres.Any(n => PalabrasPropias(NombresMaterias.Clave(n)).Overlaps(palabrasNombreDestino));
        return new Afinidad(Coseno(texto, destino), nombreEnComun, SinDatos: false);
    }

    /// <summary>
    /// Sugiere, para cada materia aprobada, la materia de destino más relacionada (cada materia de destino
    /// se usa una sola vez y se prefieren los pares más parecidos). Las aprobadas sin una materia
    /// suficientemente parecida no se sugieren.
    /// </summary>
    public IReadOnlyList<SugerenciaHomologacion> Sugerir(IEnumerable<string> aprobadas, IEnumerable<string>? yaPedidas = null)
    {
        var usadas = new HashSet<string>((yaPedidas ?? Array.Empty<string>()).Select(NombresMaterias.Clave), StringComparer.Ordinal);
        var candidatas = aprobadas
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .SelectMany(a => _nombresDestino.Select(d =>
            {
                var afinidad = Calcular(new[] { a }, d.Value);
                // El nombre en común solo desempata: lo que manda es el parecido de los temas.
                return (Origen: a, Destino: d.Value, Clave: d.Key, Afinidad: afinidad,
                    Orden: afinidad.Puntaje + (afinidad.NombreEnComun ? 0.15 : 0));
            }))
            .Where(x => x.Afinidad.Puntaje >= MinimoSugerencia || (x.Afinidad.NombreEnComun && x.Afinidad.Puntaje >= Minimo / 2))
            .OrderByDescending(x => x.Orden)
            .ToList();

        var origenesUsados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sugerencias = new List<SugerenciaHomologacion>();
        foreach (var c in candidatas)
        {
            if (origenesUsados.Contains(c.Origen) || usadas.Contains(c.Clave)) continue;
            origenesUsados.Add(c.Origen);
            usadas.Add(c.Clave);
            sugerencias.Add(new SugerenciaHomologacion(c.Origen, NombresMaterias.Titulo(c.Destino), c.Afinidad.Porcentaje));
        }

        // En el orden en que se escribieron las materias aprobadas.
        var orden = aprobadas.Select((a, i) => (a, i)).GroupBy(x => x.a, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().i, StringComparer.OrdinalIgnoreCase);
        return sugerencias.OrderBy(s => orden[s.MateriaOrigen]).ToList();
    }

    /// <summary>Nombre oficial (de la malla de destino) de la materia de destino más relacionada, o null.</summary>
    public string? MasRelacionada(IEnumerable<string> materiasOrigen)
    {
        var lista = materiasOrigen.ToList();
        var mejor = _nombresDestino.Values
            .Select(d => (Destino: d, Afinidad: Calcular(lista, d)))
            .Where(x => !x.Afinidad.SinDatos && x.Afinidad.Puntaje >= MinimoSugerencia)
            .OrderByDescending(x => x.Afinidad.Puntaje)
            .FirstOrDefault();
        return mejor.Destino is null ? null : NombresMaterias.Titulo(mejor.Destino);
    }

    private double Coseno(Dictionary<string, int> a, Dictionary<string, int> b)
    {
        double Peso(string palabra, int veces) => (1 + Math.Log(veces)) * _idf.GetValueOrDefault(palabra, Math.Log(2));
        var va = a.ToDictionary(x => x.Key, x => Peso(x.Key, x.Value), StringComparer.Ordinal);
        var vb = b.ToDictionary(x => x.Key, x => Peso(x.Key, x.Value), StringComparer.Ordinal);
        var producto = va.Sum(x => vb.TryGetValue(x.Key, out var y) ? x.Value * y : 0);
        var normas = Math.Sqrt(va.Values.Sum(v => v * v)) * Math.Sqrt(vb.Values.Sum(v => v * v));
        return normas == 0 ? 0 : Math.Min(1, producto / normas);
    }

    private static Dictionary<string, Dictionary<string, int>> Indexar(
        IEnumerable<MateriaCatalogo> malla, out Dictionary<string, string> nombres)
    {
        var indice = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        nombres = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var m in malla.Where(m => !string.IsNullOrWhiteSpace(m.Nombre)))
        {
            var clave = NombresMaterias.Clave(m.Nombre);
            if (indice.ContainsKey(clave)) continue;
            // El nombre pesa más que el texto de las unidades.
            var conteo = Contar(Palabras(m.Nombre), peso: 3);
            foreach (var (palabra, veces) in Contar(Palabras(m.Contenidos), peso: 1))
                conteo[palabra] = conteo.GetValueOrDefault(palabra) + veces;
            indice[clave] = conteo;
            nombres[clave] = m.Nombre;
        }

        return indice;
    }

    private static Dictionary<string, int> Contar(IEnumerable<string> palabras, int peso)
    {
        var conteo = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var p in palabras) conteo[p] = conteo.GetValueOrDefault(p) + peso;
        return conteo;
    }

    /// <summary>Palabras con contenido, sin tildes y recortadas a su raíz (6 letras: "programación" → "progra").</summary>
    private static IEnumerable<string> Palabras(string texto)
    {
        var sinTildes = new string((texto ?? string.Empty).Normalize(NormalizationForm.FormD)
            .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray()).ToLowerInvariant();
        return Regex.Split(sinTildes, "[^a-z]+")
            .Where(p => p.Length >= 4 && !Vacias.Contains(p))
            .Select(p => p.Length > 6 ? p[..6] : p);
    }

    private static HashSet<string> PalabrasPropias(string nombre)
        => Palabras(nombre).Where(p => !NombresGenerales.Contains(p)).ToHashSet(StringComparer.Ordinal);
}
