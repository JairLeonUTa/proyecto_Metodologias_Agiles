using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Siha.Certificados;
using Siha.Contracts;
using Siha.MsC.Api.Contracts.Dtos;
using Siha.MsC.Api.Data;
using Siha.MsC.Api.Domain.Entities;
using Siha.MsC.Api.Grpc;

namespace Siha.MsC.Api.Services;

public interface IAnalisisAutomaticoService
{
    /// <summary>
    /// Análisis de una solicitud con certificado SIHA: el agente toma el certificado guardado, trae de
    /// MS A (gRPC) las mallas y sílabos de la carrera de origen y de destino, compara cada materia que se
    /// pide homologar y guarda la estimación explicada. No lanza excepciones: si falla, deja la solicitud
    /// en ERROR con el motivo.
    /// </summary>
    Task AnalizarYGuardarAsync(Guid expedienteId, CancellationToken cancellationToken = default);
}

/// <summary>Motivo por el que el análisis no puede ejecutarse.</summary>
public sealed class AnalisisAutomaticoException : Exception
{
    public AnalisisAutomaticoException(int codigoHttp, string mensaje) : base(mensaje) => CodigoHttp = codigoHttp;

    public int CodigoHttp { get; }
}

/// <summary>
/// Agente de homologación. Conoce las mallas y sílabos oficiales de la UTA (MS A) y, por cada materia
/// que el estudiante pide homologar, mide tres criterios del instructivo (mínimo 80 % cada uno):
///  - Contenidos: horas de la materia de destino cuyos temas aparecen en lo aprobado.
///  - Profundidad: horas que lo aprobado dedica a esos temas frente a las que exige el destino.
///  - Carga horaria: horas totales aprobadas frente a las horas de la materia de destino.
/// Si los tres llegan al mínimo, la materia CUMPLE; si no, queda a CRITERIO del docente. Todo se
/// explica en lenguaje sencillo, unidad por unidad.
/// </summary>
public sealed class AnalisisAutomaticoService : IAnalisisAutomaticoService
{
    public const string Cumple = "CUMPLE";
    public const string CriterioDocente = "CRITERIO_DOCENTE";
    private const double ThetaPorDefecto = 80.0;
    private const double TauPorDefecto = 75.0;

    /// <summary>Serialización del resultado guardado (misma forma que la respuesta HTTP).</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SihaDbContextC _db;
    private readonly IReglasGrpcClient _reglas;
    private readonly IPrecedenteService _precedentes;
    private readonly IOllamaEmbeddingService _ollama;
    private readonly ILogger<AnalisisAutomaticoService> _logger;
    private readonly IEmbeddingCacheService? _cache;
    private readonly ICatalogoGrpcCliente? _catalogo;

    public AnalisisAutomaticoService(
        SihaDbContextC db,
        IReglasGrpcClient reglas,
        IPrecedenteService precedentes,
        IOllamaEmbeddingService ollama,
        ILogger<AnalisisAutomaticoService> logger,
        IEmbeddingCacheService? cache = null,
        ICatalogoGrpcCliente? catalogo = null)
    {
        _db = db;
        _reglas = reglas;
        _precedentes = precedentes;
        _ollama = ollama;
        _logger = logger;
        _cache = cache;
        _catalogo = catalogo;
    }

    public async Task AnalizarYGuardarAsync(Guid expedienteId, CancellationToken cancellationToken = default)
    {
        var contexto = await _db.AnalisisExpedientes.FirstOrDefaultAsync(a => a.ExpedienteId == expedienteId, cancellationToken);
        if (contexto is null) return; // no llegó con certificado SIHA

        contexto.Estado = EstadosAnalisis.Pendiente;
        contexto.Mensaje = null;
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var certificado = JsonSerializer.Deserialize<CertificadoSiha>(contexto.CertificadoJson);
            if (certificado?.Solicitudes is null || certificado.Materias is null)
                throw new AnalisisAutomaticoException(StatusCodes.Status409Conflict,
                    "Esta solicitud se registró con el formato anterior del certificado. Suba de nuevo el certificado con el formato actual.");
            var resultado = await AnalizarAsync(contexto, certificado, cancellationToken);
            contexto.ResultadoJson = JsonSerializer.Serialize(resultado, Json);
            contexto.Estado = EstadosAnalisis.Analizado;
            contexto.AnalizadoUtc = DateTimeOffset.UtcNow;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "El análisis automático del expediente {ExpedienteId} falló.", expedienteId);
            contexto.Estado = EstadosAnalisis.Error;
            contexto.Mensaje = ex is AnalisisAutomaticoException
                ? ex.Message
                : "El agente no pudo completar el análisis. Intente nuevamente.";
        }

        await _db.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Compara las materias pedidas en el certificado y arma la estimación explicada.</summary>
    public async Task<AnalisisAutomaticoResponse> AnalizarAsync(
        AnalisisExpediente contexto, CertificadoSiha certificado, CancellationToken cancellationToken = default)
    {
        if (_catalogo is null)
            throw new AnalisisAutomaticoException(StatusCodes.Status503ServiceUnavailable, "El catálogo de MS A no está configurado.");

        // 1) Mínimo de contenido (θ) y parecido por unidad (τ) vigentes por gRPC.
        double theta, tau;
        var traza = new List<string>();
        try
        {
            var config = await _reglas.ObtenerConfiguracionAsync(contexto.RutaNormativa, cancellationToken: cancellationToken);
            theta = config?.ParametrosConfigurados == true ? config.Parametros.Theta : ThetaPorDefecto;
            tau = config?.ParametrosConfigurados == true ? config.Parametros.Tau : TauPorDefecto;
            traza.Add($"Reglas {contexto.RutaNormativa} v{config?.Parametros?.Version}: θ={theta} τ={tau}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo consultar las reglas vigentes.");
            throw new AnalisisAutomaticoException(StatusCodes.Status503ServiceUnavailable,
                "No se pudo consultar las reglas vigentes en el servicio de reglas. Intente nuevamente.");
        }

        // 2) Mallas oficiales (sílabos) de origen y destino.
        IReadOnlyList<DestinoAnalisisRequest> mallaDestino, mallaOrigen;
        try
        {
            mallaDestino = await _catalogo.ObtenerMallaAsync(contexto.CarreraDestinoCodigo, cancellationToken);
            mallaOrigen = contexto.TipoTramite == TiposTramite.Interno
                ? await _catalogo.ObtenerMallaAsync(contexto.CarreraOrigenCodigo, cancellationToken)
                : Array.Empty<DestinoAnalisisRequest>();
        }
        catch (ReglasGrpcException ex)
        {
            throw new AnalisisAutomaticoException(StatusCodes.Status503ServiceUnavailable,
                $"No se pudo obtener las mallas desde MS A: {ex.Message}");
        }

        if (mallaDestino.Count == 0)
            throw new AnalisisAutomaticoException(StatusCodes.Status404NotFound,
                $"La carrera de destino {contexto.CarreraDestinoNombre} no tiene malla con sílabos en el catálogo.");

        traza.Add($"Malla destino {contexto.CarreraDestinoCodigo}: {mallaDestino.Count} asignaturas; origen {contexto.CarreraOrigenCodigo}: {mallaOrigen.Count}");

        // 3) Cada materia pedida.
        var evaluaciones = new List<EvaluacionMateriaResponse>();
        foreach (var solicitud in certificado.Solicitudes)
        {
            evaluaciones.Add(await EvaluarAsync(solicitud, certificado, contexto, mallaOrigen, mallaDestino, theta, tau, cancellationToken));
        }

        var requisitos = Requisitos(certificado, mallaOrigen, evaluaciones, theta, contexto);
        var cumplen = evaluaciones.Count(e => e.Estimacion == Cumple);
        var sinSilabo = evaluaciones.Count(e => e.SinSilabo);
        var criterio = evaluaciones.Count - cumplen - sinSilabo;
        var partes = new List<string>();
        if (sinSilabo < evaluaciones.Count) partes.Add($"{cumplen} cumple(n) el {Num(theta)} % en contenidos, profundidad y carga horaria");
        if (criterio > 0) partes.Add($"{criterio} queda(n) a criterio de los docentes revisores");
        if (sinSilabo > 0) partes.Add($"{sinSilabo} no tiene(n) sílabo para comparar y la(s) deciden los docentes");
        var resumen = $"Se analizaron {evaluaciones.Count} materia(s) pedida(s): {Unir(partes)}.";

        return new AnalisisAutomaticoResponse(contexto.ExpedienteId, requisitos, evaluaciones, Array.Empty<string>(), resumen, string.Join(" | ", traza));
    }

    private async Task<EvaluacionMateriaResponse> EvaluarAsync(
        SolicitudHomologacion solicitud,
        CertificadoSiha certificado,
        AnalisisExpediente contexto,
        IReadOnlyList<DestinoAnalisisRequest> mallaOrigen,
        IReadOnlyList<DestinoAnalisisRequest> mallaDestino,
        double theta,
        double tau,
        CancellationToken cancellationToken)
    {
        var destino = Buscar(mallaDestino, solicitud.MateriaDestino);
        var origenes = solicitud.MateriasOrigen.Select(nombre =>
        {
            var cursada = certificado.Materias.First(m => Clave(m.Nombre) == Clave(nombre));
            return (Cursada: cursada, Malla: Buscar(mallaOrigen, nombre));
        }).ToList();

        var origenDto = origenes.Select(o => new MateriaOrigenEvaluadaResponse(
            o.Cursada.Nombre, o.Cursada.Nivel, o.Cursada.PeriodoAcademico, o.Cursada.Nota,
            o.Malla?.Unidades.Sum(u => u.Horas), o.Malla?.Codigo, o.Malla is not null)).ToList();

        var nombresOrigen = Unir(origenes.Select(o => $"«{o.Cursada.Nombre}»"));
        var explicacion = new List<string>();

        // Sin sílabo no hay con qué comparar: lo decide el docente con la documentación. También cuenta
        // como "sin sílabo" una materia del catálogo sin unidades o sin horas (no se compara con 0 horas).
        static bool SinUnidades(DestinoAnalisisRequest? a) => a is not null && (a.Unidades.Count == 0 || a.Unidades.Sum(u => u.Horas) <= 0);
        if (destino is null || SinUnidades(destino) || origenes.Any(o => o.Malla is null || SinUnidades(o.Malla)))
        {
            if (destino is null)
                explicacion.Add($"La materia «{solicitud.MateriaDestino}» no existe en la malla vigente de {contexto.CarreraDestinoNombre}; revise que el nombre sea el oficial.");
            else if (SinUnidades(destino))
                explicacion.Add($"El sílabo de «{solicitud.MateriaDestino}» en {contexto.CarreraDestinoNombre} no tiene unidades publicadas.");
            foreach (var o in origenes.Where(o => o.Malla is not null && SinUnidades(o.Malla)))
                explicacion.Add($"El sílabo de «{o.Cursada.Nombre}» en {contexto.CarreraOrigenNombre} no tiene unidades publicadas.");
            foreach (var o in origenes.Where(o => o.Malla is null))
                explicacion.Add(contexto.TipoTramite == TiposTramite.Interno
                    ? $"La materia «{o.Cursada.Nombre}» no aparece en la malla de {contexto.CarreraOrigenNombre}, así que el agente no tiene su sílabo."
                    : $"La materia «{o.Cursada.Nombre}» es de otra institución ({contexto.InstitucionOrigen}); el agente no tiene su sílabo.");
            return new EvaluacionMateriaResponse(solicitud.Numero, solicitud.MateriaDestino, destino?.Codigo, destino?.Semestre, destino?.Creditos,
                destino?.Unidades.Sum(u => u.Horas) ?? 0, origenDto, 0, 0, 0, CriterioDocente, Array.Empty<CoberturaUnidadPropuestaResponse>(),
                explicacion, "Sin sílabo para comparar: queda a criterio de los docentes revisores, con el plan analítico que presente el estudiante.",
                Array.Empty<PrecedenteResponse>(), SinSilabo: true);
        }

        // Vectores (caché EMBEDDING_UNIDAD por asignatura y unidad; Ollama si no están).
        var unidadesOrigen = new List<(string Materia, UnidadCatalogoRequest Unidad, float[] Vector)>();
        foreach (var o in origenes)
            foreach (var u in o.Malla!.Unidades.OrderBy(u => u.Numero))
                unidadesOrigen.Add((o.Cursada.Nombre, u, await VectorAsync(o.Malla.Codigo, u, cancellationToken)));

        var horasDestino = destino.Unidades.Sum(u => u.Horas);
        var horasOrigen = origenes.Sum(o => o.Malla!.Unidades.Sum(u => u.Horas));
        explicacion.Add($"«{solicitud.MateriaDestino}» de {contexto.CarreraDestinoNombre} (nivel {destino.Semestre}, {destino.Creditos} créditos) tiene " +
                        $"{destino.Unidades.Count} unidades y {horasDestino} horas. Se comparó con {nombresOrigen} de {contexto.CarreraOrigenNombre} " +
                        $"({horasOrigen} horas), aprobada(s) en {Unir(origenes.Select(o => $"{o.Cursada.PeriodoAcademico} con nota {Num((double)o.Cursada.Nota)}"))}.");

        var unidades = new List<CoberturaUnidadPropuestaResponse>();
        double cubiertas = 0, horasEnTemasCubiertos = 0, horasExigidasCubiertas = 0;
        foreach (var d in destino.Unidades.OrderBy(u => u.Numero))
        {
            var vector = await VectorAsync(destino.Codigo, d, cancellationToken);
            var mejor = unidadesOrigen
                .Select(o => (o.Materia, o.Unidad, Similitud: Coseno(o.Vector, vector)))
                .OrderByDescending(x => x.Similitud)
                .FirstOrDefault();
            var cubierta = mejor.Unidad is not null && mejor.Similitud >= tau / 100.0;
            var similitud = Math.Round(mejor.Similitud * 100, 2);
            unidades.Add(new CoberturaUnidadPropuestaResponse(d.Numero, d.Nombre, d.Horas, similitud, cubierta,
                mejor.Materia, mejor.Unidad?.Numero, mejor.Unidad?.Nombre, mejor.Unidad?.Horas));

            if (cubierta)
            {
                cubiertas += d.Horas;
                horasExigidasCubiertas += d.Horas;
                horasEnTemasCubiertos += Math.Min(mejor.Unidad!.Horas, d.Horas);
                explicacion.Add($"✔ Unidad {d.Numero} «{Corto(d.Nombre)}» ({d.Horas} h): la cubre la unidad {mejor.Unidad.Numero} «{Corto(mejor.Unidad.Nombre)}» de «{mejor.Materia}» ({mejor.Unidad.Horas} h), con {Num(similitud)} % de parecido en los temas.");
            }
            else
            {
                explicacion.Add(mejor.Unidad is null
                    ? $"✘ Unidad {d.Numero} «{Corto(d.Nombre)}» ({d.Horas} h): no hay temas aprobados con qué compararla."
                    : $"✘ Unidad {d.Numero} «{Corto(d.Nombre)}» ({d.Horas} h): no aparece en lo aprobado. Lo más parecido es la unidad {mejor.Unidad.Numero} «{Corto(mejor.Unidad.Nombre)}» de «{mejor.Materia}», con solo {Num(similitud)} % de parecido.");
            }
        }

        var contenidos = Porcentaje(cubiertas, horasDestino);
        var profundidad = Porcentaje(horasEnTemasCubiertos, horasExigidasCubiertas);
        var carga = Math.Min(100m, Porcentaje(horasOrigen, horasDestino));

        explicacion.Add($"Contenidos: se cubren {Num(cubiertas)} de {horasDestino} horas de la materia de destino = {Num((double)contenidos)} %.");
        explicacion.Add(horasExigidasCubiertas > 0
            ? $"Profundidad: en los temas que coinciden, lo aprobado dedica {Num(horasEnTemasCubiertos)} horas de las {Num(horasExigidasCubiertas)} que exige {contexto.CarreraDestinoNombre} = {Num((double)profundidad)} %."
            : "Profundidad: no hay temas que coincidan, así que no se puede medir.");
        explicacion.Add($"Carga horaria: {horasOrigen} horas aprobadas frente a {horasDestino} horas de la materia de destino = {Num((double)carga)} %.");

        var bajos = new List<string>();
        if ((double)contenidos < theta) bajos.Add($"contenidos ({Num((double)contenidos)} %)");
        if ((double)profundidad < theta) bajos.Add($"profundidad ({Num((double)profundidad)} %)");
        if ((double)carga < theta) bajos.Add($"carga horaria ({Num((double)carga)} %)");
        var estimacion = bajos.Count == 0 ? Cumple : CriterioDocente;
        var conclusion = estimacion == Cumple
            ? $"Cumple el {Num(theta)} % en contenidos, profundidad y carga horaria: el agente recomienda homologar «{solicitud.MateriaDestino}»."
            : $"No llega al {Num(theta)} % en {Unir(bajos)}: queda a criterio de los docentes revisores.";

        var precedentes = await _precedentes.BuscarAsync(new BuscarPrecedenteRequest(
            origenes.Select(o => o.Malla!.Codigo).ToList(), destino.Codigo, contexto.InstitucionOrigen), cancellationToken);
        if (precedentes.Count > 0)
            explicacion.Add($"Hay {precedentes.Count} caso(s) anterior(es) idéntico(s): {string.Join(", ", precedentes.Select(p => p.Resolucion))}.");

        return new EvaluacionMateriaResponse(solicitud.Numero, solicitud.MateriaDestino, destino.Codigo, destino.Semestre, destino.Creditos,
            horasDestino, origenDto, contenidos, profundidad, carga, estimacion, unidades, explicacion, conclusion, precedentes);
    }

    /// <summary>Reglas del instructivo de cambio de carrera, verificadas con el certificado y la malla de origen.</summary>
    private static List<RequisitoResponse> Requisitos(
        CertificadoSiha c, IReadOnlyList<DestinoAnalisisRequest> mallaOrigen, List<EvaluacionMateriaResponse> evaluaciones,
        double theta, AnalisisExpediente contexto)
    {
        var requisitos = new List<RequisitoResponse>();

        var primerNivel = mallaOrigen.Where(a => a.Semestre == 1).ToList();
        if (primerNivel.Count > 0)
        {
            var faltan = primerNivel.Where(a => !c.Materias.Any(m => m.Aprobada && Parecidas(m.Nombre, a.Nombre))).Select(a => Titulo(a.Nombre)).ToList();
            requisitos.Add(new RequisitoResponse("Aprobó al menos el primer período académico", faltan.Count == 0,
                faltan.Count == 0
                    ? $"Aprobó las {primerNivel.Count} materias del primer nivel de {contexto.CarreraOrigenNombre}."
                    : $"En el certificado no constan aprobadas estas materias del primer nivel de {contexto.CarreraOrigenNombre}: {string.Join(", ", faltan)}."));
        }
        else
        {
            var n1 = c.Materias.Count(m => m.Nivel == 1);
            requisitos.Add(new RequisitoResponse("Aprobó al menos el primer período académico", true,
                $"El certificado registra {n1} materia(s) de primer nivel, todas aprobadas."));
        }

        var reprobadas = c.Materias.Where(m => !m.Aprobada).ToList();
        requisitos.Add(new RequisitoResponse("Solo se piden materias aprobadas", true,
            reprobadas.Count == 0
                ? "Todas las materias del historial están aprobadas."
                : $"Reprobó {Unir(reprobadas.Select(m => $"«{m.Nombre}» (nivel {m.Nivel}, nota {Num((double)m.Nota)})"))}, pero no las pide homologar. Reprobar materias después del primer período no impide el cambio."));

        var cumplen = evaluaciones.Count(e => e.Estimacion == Cumple);
        var comparables = evaluaciones.Where(e => !e.SinSilabo).ToList();
        requisitos.Add(new RequisitoResponse("Tiene al menos una materia susceptible de homologación", cumplen > 0,
            cumplen > 0
                ? $"{cumplen} de {evaluaciones.Count} materia(s) pedida(s) llegan al {Num(theta)} %."
                : comparables.Count == 0
                    ? "El agente no tiene sílabos para comparar estas materias; los docentes deciden con el plan analítico del estudiante."
                    : $"Ninguna materia pedida llega al {Num(theta)} %; los docentes deciden si alguna procede."));

        requisitos.Add(new RequisitoResponse("Materias vigentes y sin tercera matrícula", true,
            "El certificado de Secretaría solo incluye materias de los últimos 10 años y sin tercera matrícula."));

        // Las materias sin sílabo no cuentan como que fallaron el mínimo: no se pudieron medir.
        var detalle = evaluaciones.Select(e => e.SinSilabo
            ? $"«{e.MateriaDestino}»: sin sílabo para comparar (deciden los docentes)."
            : $"«{e.MateriaDestino}»: {Estado(e.Estimacion)} (contenidos {Num((double)e.Contenidos)} %, " +
              $"profundidad {Num((double)e.Profundidad)} %, carga horaria {Num((double)e.CargaHoraria)} %).");
        requisitos.Add(new RequisitoResponse($"Equivalencia mínima del {Num(theta)} % en contenidos, profundidad y carga horaria",
            comparables.All(e => e.Estimacion == Cumple), string.Join(" ", detalle)));
        return requisitos;
    }

    private static string Estado(string estimacion) => estimacion == Cumple ? "cumple" : "a criterio del docente";

    private async Task<float[]> VectorAsync(string codigoAsignatura, UnidadCatalogoRequest u, CancellationToken cancellationToken)
    {
        // Nombre de la unidad + sus temas: con ambos el parecido distingue mejor las materias afines.
        var texto = $"{u.Nombre}. {u.Contenidos}".Trim();
        var vector = _cache is not null
            ? await _cache.ObtenerAsync(codigoAsignatura, u.Numero, texto, cancellationToken)
            : await _ollama.GenerarEmbeddingAsync(texto, cancellationToken);
        return vector is { Length: > 0 }
            ? vector
            : throw new AnalisisAutomaticoException(StatusCodes.Status503ServiceUnavailable,
                "El servicio de IA (Ollama) no respondió: no se pudo comparar los contenidos.");
    }

    /// <summary>Busca una asignatura en la malla por su nombre (sin tildes ni mayúsculas).</summary>
    private static DestinoAnalisisRequest? Buscar(IReadOnlyList<DestinoAnalisisRequest> malla, string nombre)
        => malla.FirstOrDefault(a => Clave(a.Nombre) == Clave(nombre))
           ?? malla.FirstOrDefault(a => Parecidas(a.Nombre, nombre));

    private static bool Parecidas(string a, string b)
    {
        var x = Clave(a).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var y = Clave(b).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        if (x.Count == 0 || y.Count == 0) return false;
        return x.SetEquals(y) || (double)x.Intersect(y).Count() / x.Union(y).Count() >= 0.75;
    }

    private static string Clave(string texto)
    {
        var sinTildes = new string(texto.Normalize(NormalizationForm.FormD)
            .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray()).ToLowerInvariant();
        return string.Join(" ", Regex.Split(sinTildes, "[^a-z0-9]+").Where(p => p.Length > 0));
    }

    /// <summary>"ALGORITMOS Y LOGICA" → "Algoritmos y Logica" para que se lea bien en las explicaciones.</summary>
    private static string Titulo(string texto)
    {
        if (texto.Any(char.IsLower)) return texto;
        var menores = new HashSet<string> { "y", "de", "del", "la", "las", "los", "el", "a", "en", "e", "para", "por" };
        var palabras = texto.ToLowerInvariant().Split(' ');
        return string.Join(" ", palabras.Select((p, i) => i > 0 && menores.Contains(p) ? p : p.Length > 0 ? char.ToUpperInvariant(p[0]) + p[1..] : p));
    }

    private static string Corto(string texto) => texto.Length <= 90 ? texto : texto[..87].TrimEnd() + "…";

    private static string Unir(IEnumerable<string> partes)
    {
        var lista = partes.ToList();
        return lista.Count <= 1 ? string.Concat(lista) : string.Join(", ", lista.Take(lista.Count - 1)) + " y " + lista[^1];
    }

    private static decimal Porcentaje(double parte, double total)
        => total <= 0 ? 0 : (decimal)Math.Round(parte / total * 100, 2);

    private static string Num(double n) => n.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',');

    private static double Coseno(float[] a, float[] b)
    {
        if (a.Length == 0 || a.Length != b.Length) return 0;
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }

        var den = Math.Sqrt(na) * Math.Sqrt(nb);
        return den == 0 ? 0 : dot / den;
    }
}
