using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Siha.Contracts;
using Siha.MsC.Api.Contracts.Dtos;
using Siha.MsC.Api.Data;
using Siha.MsC.Api.Domain;
using Siha.MsC.Api.Domain.Entities;
using Siha.MsC.Api.Grpc;

namespace Siha.MsC.Api.Services;

public interface IAnalisisAutomaticoService
{
    Task<AnalisisAutomaticoResponse> AnalizarAsync(
        Guid expedienteId, AnalisisAutomaticoRequest solicitud, CancellationToken cancellationToken = default);

    /// <summary>
    /// Análisis automático de una solicitud con certificado SIHA: el agente toma el contexto guardado,
    /// pide a MS A la malla vigente de la carrera de destino, compara y guarda la estimación.
    /// No lanza excepciones: si falla, deja la solicitud en ERROR con el motivo.
    /// </summary>
    Task AnalizarYGuardarAsync(Guid expedienteId, CancellationToken cancellationToken = default);
}

/// <summary>Motivo por el que el análisis automático no puede ejecutarse (el controlador lo traduce a HTTP).</summary>
public sealed class AnalisisAutomaticoException : Exception
{
    public AnalisisAutomaticoException(int codigoHttp, string mensaje, IReadOnlyCollection<string>? detalles = null)
        : base(mensaje)
    {
        CodigoHttp = codigoHttp;
        Detalles = detalles ?? Array.Empty<string>();
    }

    public int CodigoHttp { get; }
    public IReadOnlyCollection<string> Detalles { get; }
}

/// <summary>
/// Agente de homologación automática por contenidos.
///
/// 1. Toma todas las materias aprobadas del expediente (extracciones CONFIRMADAS, vienen del certificado).
/// 2. Pide por gRPC a MS A los umbrales vigentes de la ruta (θ mínimo de contenido, τ por unidad).
/// 3. Para cada materia de la carrera de destino busca el menor grupo de materias aprobadas cuyas
///    unidades cubren al menos θ % de sus horas (N:1). Una materia aprobada se usa una sola vez (RN-19).
/// 4. Para cada propuesta calcula créditos, vigencia (años desde la aprobación), matrícula y precedentes.
///
/// Ejemplo: Física I de Software reúne Física I y II de Industrial. Con solo Física I de Industrial
/// se cubre alrededor de la mitad de sus horas y NO se propone; con ambas sí.
/// </summary>
public sealed class AnalisisAutomaticoService : IAnalisisAutomaticoService
{
    private const double ThetaPorDefecto = 80.0;
    private const double TauPorDefecto = 75.0;

    private readonly SihaDbContextC _db;
    private readonly IReglasGrpcClient _reglas;
    private readonly IReglaCreditosStrategy _reglaCreditos;
    private readonly IReglaContenidoStrategy _reglaContenido;
    private readonly IPrecedenteService _precedentes;
    private readonly IOllamaEmbeddingService _ollama;
    private readonly ILogger<AnalisisAutomaticoService> _logger;
    private readonly IEmbeddingCacheService? _cache;
    private readonly ICatalogoGrpcCliente? _catalogo;

    public AnalisisAutomaticoService(
        SihaDbContextC db,
        IReglasGrpcClient reglas,
        IReglaCreditosStrategy reglaCreditos,
        IReglaContenidoStrategy reglaContenido,
        IPrecedenteService precedentes,
        IOllamaEmbeddingService ollama,
        ILogger<AnalisisAutomaticoService> logger,
        IEmbeddingCacheService? cache = null,
        ICatalogoGrpcCliente? catalogo = null)
    {
        _catalogo = catalogo;
        _db = db;
        _reglas = reglas;
        _reglaCreditos = reglaCreditos;
        _reglaContenido = reglaContenido;
        _precedentes = precedentes;
        _ollama = ollama;
        _logger = logger;
        _cache = cache;
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
            if (_catalogo is null)
                throw new AnalisisAutomaticoException(StatusCodes.Status503ServiceUnavailable, "El catálogo de MS A no está configurado.");

            IReadOnlyList<DestinoAnalisisRequest> malla;
            try
            {
                malla = await _catalogo.ObtenerMallaAsync(contexto.CarreraDestinoCodigo, cancellationToken);
            }
            catch (ReglasGrpcException ex)
            {
                throw new AnalisisAutomaticoException(StatusCodes.Status503ServiceUnavailable,
                    $"No se pudo obtener la malla de la carrera de destino desde MS A: {ex.Message}");
            }

            if (malla.Count == 0)
                throw new AnalisisAutomaticoException(StatusCodes.Status404NotFound,
                    $"La carrera de destino {contexto.CarreraDestinoCodigo} no tiene una malla con asignaturas en el catálogo.");

            var origen = await _db.Extracciones.AsNoTracking()
                .Where(e => e.ExpedienteId == expedienteId)
                .Select(e => new OrigenAnalisisRequest(e.CodigoAsignaturaOrigen, e.FechaAprobacion, null))
                .ToListAsync(cancellationToken);

            var resultado = await AnalizarAsync(expedienteId, new AnalisisAutomaticoRequest(
                contexto.RutaNormativa, contexto.TipoTramite, contexto.InstitucionOrigen, origen, malla), cancellationToken);

            contexto.ResultadoJson = System.Text.Json.JsonSerializer.Serialize(resultado, Json);
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

    /// <summary>Serialización del resultado guardado (misma forma que la respuesta HTTP).</summary>
    public static readonly System.Text.Json.JsonSerializerOptions Json = new(System.Text.Json.JsonSerializerDefaults.Web);

    public async Task<AnalisisAutomaticoResponse> AnalizarAsync(
        Guid expedienteId, AnalisisAutomaticoRequest solicitud, CancellationToken cancellationToken = default)
    {
        Validar(solicitud);
        var alertas = new List<string>();
        var traza = new List<string>();

        // 1) Materias aprobadas del expediente
        var extracciones = await _db.Extracciones
            .Include(e => e.Unidades)
            .AsNoTracking()
            .Where(e => e.ExpedienteId == expedienteId)
            .OrderBy(e => e.CodigoAsignaturaOrigen)
            .ToListAsync(cancellationToken);

        if (extracciones.Count == 0)
        {
            throw new AnalisisAutomaticoException(StatusCodes.Status404NotFound,
                "El expediente todavía no tiene materias leídas. Espere a que termine el procesamiento del certificado.");
        }

        var sinConfirmar = extracciones.Where(e => e.Estado != EstadosExtraccion.Confirmada).ToList();
        if (sinConfirmar.Count > 0)
        {
            throw new AnalisisAutomaticoException(StatusCodes.Status409Conflict,
                "Hay materias del expediente que no están confirmadas.",
                sinConfirmar.Select(e => $"{e.CodigoAsignaturaOrigen} {e.NombreAsignaturaOrigen}: {e.Estado}").ToList());
        }

        // 2) Umbrales vigentes por gRPC (θ y τ no se muestran en pantalla, pero deciden)
        ConfiguracionRutaUmbrales umbrales;
        try
        {
            var config = await _reglas.ObtenerConfiguracionAsync(solicitud.RutaNormativa, cancellationToken: cancellationToken);
            umbrales = config?.ParametrosConfigurados == true
                ? new ConfiguracionRutaUmbrales(config.Parametros.Theta, config.Parametros.Tau, config.Parametros.Version)
                : new ConfiguracionRutaUmbrales(ThetaPorDefecto, TauPorDefecto, 0);
            traza.Add(config?.ParametrosConfigurados == true
                ? $"Reglas gRPC {solicitud.RutaNormativa} v{umbrales.Version}: θ={umbrales.Theta} τ={umbrales.Tau}"
                : $"Reglas gRPC {solicitud.RutaNormativa}: sin parámetros, se usan θ={ThetaPorDefecto} τ={TauPorDefecto}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo consultar las reglas vigentes para el expediente {ExpedienteId}", expedienteId);
            throw new AnalisisAutomaticoException(StatusCodes.Status503ServiceUnavailable,
                "No se pudo consultar las reglas vigentes en el servicio de reglas. Intente nuevamente.");
        }

        // 3) Vectores de las unidades de origen (una sola vez por unidad)
        var origenes = new List<OrigenVectorizado>();
        var metadatos = solicitud.AsignaturasOrigen
            .GroupBy(o => o.Codigo.Trim().ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.First());
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var extraccion in extracciones)
        {
            var unidades = new List<UnidadVectorizada>();
            foreach (var u in extraccion.Unidades.OrderBy(u => u.Numero))
            {
                var vector = await _ollama.GenerarEmbeddingAsync(
                    string.IsNullOrWhiteSpace(u.Contenidos) ? u.Nombre : u.Contenidos, cancellationToken);
                if (vector is { Length: > 0 })
                    unidades.Add(new UnidadVectorizada(u.Numero, u.Nombre, u.Horas, u.Contenidos, vector));
                else
                    alertas.Add($"{extraccion.CodigoAsignaturaOrigen} unidad {u.Numero} ({u.Nombre}): la IA no pudo leer sus contenidos.");
            }

            metadatos.TryGetValue(extraccion.CodigoAsignaturaOrigen.Trim().ToUpperInvariant(), out var meta);
            int? anos = meta?.FechaAprobacion is { } fecha ? AnosCompletos(fecha, hoy) : null;
            if (meta?.FechaAprobacion is null)
                alertas.Add($"{extraccion.CodigoAsignaturaOrigen}: no consta la fecha de aprobación; no se pudo controlar la vigencia.");

            origenes.Add(new OrigenVectorizado(extraccion, unidades, meta?.FechaAprobacion, anos, meta?.NumeroMatricula));
        }

        if (origenes.All(o => o.Unidades.Count == 0))
        {
            throw new AnalisisAutomaticoException(StatusCodes.Status503ServiceUnavailable,
                "El servicio de IA (Ollama) no respondió: no se pudo comparar ningún contenido.");
        }

        // 4) Vectores de las unidades de destino (caché EMBEDDING_UNIDAD → Ollama)
        var destinos = new List<DestinoVectorizado>();
        foreach (var d in solicitud.AsignaturasDestino.Where(d => d.Unidades.Count > 0 && d.Creditos > 0))
        {
            var unidades = new List<UnidadVectorizada>();
            var completo = true;
            foreach (var u in d.Unidades.OrderBy(u => u.Numero))
            {
                var texto = string.IsNullOrWhiteSpace(u.Contenidos) ? u.Nombre : u.Contenidos!;
                var vector = _cache is not null
                    ? await _cache.ObtenerAsync(d.Codigo, u.Numero, texto, cancellationToken)
                    : await _ollama.GenerarEmbeddingAsync(texto, cancellationToken);
                if (vector is not { Length: > 0 })
                {
                    completo = false;
                    break;
                }

                unidades.Add(new UnidadVectorizada(u.Numero, u.Nombre, u.Horas, texto, vector));
            }

            if (!completo || unidades.Sum(u => u.Horas) <= 0)
            {
                alertas.Add($"{d.Codigo} {d.Nombre}: no se pudo comparar (unidades sin horas o sin respuesta de la IA).");
                continue;
            }

            destinos.Add(new DestinoVectorizado(d, unidades));
        }

        traza.Add($"Origen: {origenes.Count} materia(s); destino: {destinos.Count} materia(s) comparables");

        // 5) Mejor grupo de origen para cada destino y asignación sin reutilizar materias (RN-19)
        var aceptadas = new Dictionary<string, (DestinoVectorizado Destino, List<OrigenVectorizado> Grupo)>();
        var usadas = new HashSet<Guid>();
        var pendientes = destinos.ToList();
        var huboCambio = true;
        while (huboCambio)
        {
            huboCambio = false;
            var candidatos = pendientes
                .Select(d => (Destino: d, Grupo: MejorGrupo(d, origenes.Where(o => !usadas.Contains(o.Extraccion.Id)).ToList(), umbrales)))
                .Where(c => c.Grupo.Count > 0 && Cobertura(c.Destino, c.Grupo, umbrales.Tau) >= umbrales.Theta)
                .OrderByDescending(c => Cobertura(c.Destino, c.Grupo, umbrales.Tau))
                .ThenBy(c => c.Grupo.Count)
                .ThenBy(c => c.Destino.Solicitud.Semestre ?? int.MaxValue)
                .ToList();

            foreach (var (destino, grupo) in candidatos)
            {
                if (grupo.Any(o => usadas.Contains(o.Extraccion.Id))) continue;
                aceptadas[destino.Solicitud.Codigo] = (destino, grupo);
                foreach (var o in grupo) usadas.Add(o.Extraccion.Id);
                pendientes.Remove(destino);
                huboCambio = true;
            }
        }

        // 6) Evaluación completa de cada propuesta
        var propuestas = new List<PropuestaHomologacionResponse>();
        foreach (var (destino, grupo) in aceptadas.Values.OrderBy(a => a.Destino.Solicitud.Semestre ?? int.MaxValue).ThenBy(a => a.Destino.Solicitud.Codigo))
        {
            propuestas.Add(await EvaluarAsync(destino, grupo, solicitud, umbrales, cancellationToken));
        }

        // 7) Destinos que solo tienen coincidencias parciales: se informan como NO homologables
        // (solo con materias que no quedaron asignadas a otra propuesta).
        var libres = origenes.Where(o => !usadas.Contains(o.Extraccion.Id)).ToList();
        var sinCobertura = new List<PropuestaHomologacionResponse>();
        foreach (var destino in pendientes)
        {
            var grupo = MejorGrupo(destino, libres, umbrales);
            if (grupo.Count == 0) continue;
            var resultado = await EvaluarAsync(destino, grupo, solicitud, umbrales, cancellationToken, buscarPrecedentes: false);
            if (resultado.Pcont > 0) sinCobertura.Add(resultado);
        }

        var sinAsignar = origenes
            .Where(o => !usadas.Contains(o.Extraccion.Id))
            .Select(AOrigenDto)
            .ToList();

        traza.Add($"Propuestas={propuestas.Count} Parciales={sinCobertura.Count} SinAsignar={sinAsignar.Count}");
        _logger.LogInformation(
            "Análisis automático del expediente {ExpedienteId}: {Propuestas} propuesta(s), {Parciales} parcial(es).",
            expedienteId, propuestas.Count, sinCobertura.Count);

        return new AnalisisAutomaticoResponse(
            expedienteId,
            propuestas,
            sinCobertura.OrderByDescending(p => p.Pcont).ToList(),
            sinAsignar,
            alertas.Distinct().ToList(),
            string.Join(" | ", traza));
    }

    /// <summary>
    /// Grupo mínimo de materias de origen para un destino: parte de la que más cubre y agrega las que
    /// aumentan la cobertura hasta alcanzar θ. Luego quita las que no aportan.
    /// </summary>
    private static List<OrigenVectorizado> MejorGrupo(
        DestinoVectorizado destino, List<OrigenVectorizado> disponibles, ConfiguracionRutaUmbrales umbrales)
    {
        var utiles = disponibles
            .Where(o => o.Unidades.Count > 0)
            .Select(o => (Origen: o, Cobertura: Cobertura(destino, new[] { o }, umbrales.Tau)))
            .Where(x => x.Cobertura > 0)
            .OrderByDescending(x => x.Cobertura)
            .ThenBy(x => x.Origen.Extraccion.CodigoAsignaturaOrigen, StringComparer.Ordinal)
            .Select(x => x.Origen)
            .ToList();

        if (utiles.Count == 0) return new List<OrigenVectorizado>();

        var grupo = new List<OrigenVectorizado> { utiles[0] };
        var actual = Cobertura(destino, grupo, umbrales.Tau);
        while (actual < umbrales.Theta)
        {
            var mejor = utiles
                .Where(o => !grupo.Contains(o))
                .Select(o => (Origen: o, Cobertura: Cobertura(destino, grupo.Append(o), umbrales.Tau)))
                .OrderByDescending(x => x.Cobertura)
                .FirstOrDefault();
            if (mejor.Origen is null || mejor.Cobertura <= actual) break;
            grupo.Add(mejor.Origen);
            actual = mejor.Cobertura;
        }

        foreach (var o in grupo.ToList())
        {
            if (grupo.Count == 1) break;
            var sin = grupo.Where(x => x != o).ToList();
            if (Cobertura(destino, sin, umbrales.Tau) >= actual)
            {
                grupo = sin;
            }
        }

        return grupo;
    }

    /// <summary>Porcentaje de horas del destino cubiertas por las unidades del grupo (misma fórmula que Pcont).</summary>
    private static double Cobertura(DestinoVectorizado destino, IEnumerable<OrigenVectorizado> grupo, double tau)
    {
        var unidadesOrigen = grupo.SelectMany(o => o.Unidades).ToList();
        double cubiertas = 0, total = 0;
        foreach (var d in destino.Unidades)
        {
            total += d.Horas;
            if (unidadesOrigen.Count > 0 && unidadesOrigen.Max(o => Coseno(o.Vector, d.Vector)) >= tau / 100.0)
                cubiertas += d.Horas;
        }

        return total > 0 ? Math.Round(cubiertas / total * 100, 2) : 0;
    }

    private async Task<PropuestaHomologacionResponse> EvaluarAsync(
        DestinoVectorizado destino,
        List<OrigenVectorizado> grupo,
        AnalisisAutomaticoRequest solicitud,
        ConfiguracionRutaUmbrales umbrales,
        CancellationToken cancellationToken,
        bool buscarPrecedentes = true)
    {
        // Las unidades de varias materias se numeran en secuencia para la estrategia de contenidos
        // y se guarda de qué materia viene cada una para mostrarlo.
        var mapa = new Dictionary<int, (OrigenVectorizado Origen, UnidadVectorizada Unidad)>();
        var unidadesOrigen = new List<UnidadOrigenEmbedding>();
        var indice = 1;
        foreach (var o in grupo)
        {
            foreach (var u in o.Unidades)
            {
                mapa[indice] = (o, u);
                unidadesOrigen.Add(new UnidadOrigenEmbedding(indice, u.Nombre, u.Horas, u.Contenidos, u.Vector));
                indice++;
            }
        }

        var contenido = _reglaContenido.Calcular(new ParametrosContenido(
            unidadesOrigen,
            destino.Unidades.Select(u => new UnidadDestinoEmbedding(u.Numero, u.Nombre, u.Horas, u.Contenidos, u.Vector)).ToList(),
            umbrales.Tau,
            solicitud.RutaNormativa,
            // RN-19 se controla por materia completa en la asignación (una materia aprobada, un destino).
            ValidarRN19: false));

        var anos = grupo.Max(o => o.AnosTranscurridos ?? 0);
        var matricula = Math.Max(1, grupo.Max(o => o.NumeroMatricula ?? 1));
        var creditos = _reglaCreditos.Calcular(new ParametrosCreditos(
            CreditosOrigen: grupo.Sum(o => o.Extraccion.Creditos ?? 0),
            CreditosDestino: destino.Solicitud.Creditos,
            AnosTranscurridos: anos,
            NumeroMatriculaDestino: matricula,
            EsCreditoExterno: string.Equals(solicitud.TipoTramite, TiposTramite.Externo, StringComparison.OrdinalIgnoreCase),
            RutaNormativa: solicitud.RutaNormativa));

        var alcanza = (double)contenido.Pcont >= umbrales.Theta;
        var estado = creditos.BloqueoDuro ? EstadoEvaluacionFinal.BloqueoTerceraMatricula
            : !alcanza ? EstadoEvaluacionFinal.ContenidoInsuficiente
            : creditos.Categoria == "VALIDACION_CONOCIMIENTOS" ? EstadoEvaluacionFinal.RequiereValidacionConocimientos
            : EstadoEvaluacionFinal.ListaParaDictamen;

        var alertas = creditos.Alertas.ToList();
        var faltantes = contenido.UnidadesFaltantes.Select(u => u.Nombre).ToList();
        if (!alcanza)
        {
            var nombres = string.Join(" + ", grupo.Select(o => $"{o.Extraccion.CodigoAsignaturaOrigen} {o.Extraccion.NombreAsignaturaOrigen}"));
            alertas.Insert(0, $"Con {nombres} cubre el {contenido.Pcont.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',')} % del contenido de {destino.Solicitud.Nombre}, menos del {umbrales.Theta.ToString("0.##", CultureInfo.InvariantCulture)} % requerido; queda a criterio del docente."
                              + (faltantes.Count > 0 ? $" Falta: {string.Join(", ", faltantes)}." : string.Empty));
        }

        var precedentes = buscarPrecedentes
            ? await _precedentes.BuscarAsync(new BuscarPrecedenteRequest(
                grupo.Select(o => o.Extraccion.CodigoAsignaturaOrigen).ToList(),
                destino.Solicitud.Codigo,
                solicitud.InstitucionOrigen), cancellationToken)
            : Array.Empty<PrecedenteResponse>();

        var unidades = contenido.UnidadesCubiertas.Concat(contenido.UnidadesFaltantes)
            .OrderBy(u => u.Numero)
            .Select(u =>
            {
                var origen = u.UnidadesOrigenContribuyentes.Count > 0 && mapa.TryGetValue(u.UnidadesOrigenContribuyentes.First(), out var m)
                    ? m
                    : ((OrigenVectorizado Origen, UnidadVectorizada Unidad)?)null;
                return new CoberturaUnidadPropuestaResponse(
                    u.Numero, u.Nombre, u.Horas, u.SimilitudMaxima, u.Cubierta,
                    origen?.Origen.Extraccion.CodigoAsignaturaOrigen,
                    origen?.Unidad.Numero,
                    origen?.Unidad.Nombre);
            })
            .ToList();

        return new PropuestaHomologacionResponse(
            destino.Solicitud.Codigo,
            destino.Solicitud.Nombre,
            destino.Solicitud.Creditos,
            destino.Solicitud.Semestre,
            grupo.Select(AOrigenDto).ToList(),
            contenido.Pcont,
            creditos.Pcred,
            creditos.Categoria,
            alcanza,
            estado,
            unidades,
            alertas,
            precedentes);
    }

    private static OrigenPropuestaResponse AOrigenDto(OrigenVectorizado o) => new(
        o.Extraccion.Id,
        o.Extraccion.CodigoAsignaturaOrigen,
        o.Extraccion.NombreAsignaturaOrigen,
        o.Extraccion.Creditos,
        o.Extraccion.Horas,
        o.FechaAprobacion,
        o.AnosTranscurridos,
        o.NumeroMatricula);

    private static int AnosCompletos(DateOnly desde, DateOnly hasta)
    {
        var anos = hasta.Year - desde.Year;
        if (hasta < desde.AddYears(anos)) anos--;
        return Math.Max(0, anos);
    }

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

    private static void Validar(AnalisisAutomaticoRequest s)
    {
        var errores = new List<string>();
        if (s is null) throw new AnalisisAutomaticoException(StatusCodes.Status400BadRequest, "La solicitud es obligatoria.");
        if (!RutasNormativas.Todas.Contains(s.RutaNormativa)) errores.Add("Ruta normativa inválida.");
        if (!TiposTramite.Todos.Contains(s.TipoTramite)) errores.Add("Tipo de trámite inválido.");
        if (s.AsignaturasDestino is null || s.AsignaturasDestino.Count == 0)
            errores.Add("La carrera de destino no tiene asignaturas en el catálogo.");
        if (errores.Count > 0)
            throw new AnalisisAutomaticoException(StatusCodes.Status400BadRequest, "Solicitud de análisis inválida.", errores);
    }

    private sealed record ConfiguracionRutaUmbrales(double Theta, double Tau, int Version);

    private sealed record UnidadVectorizada(int Numero, string Nombre, int Horas, string Contenidos, float[] Vector);

    private sealed record OrigenVectorizado(
        Extraccion Extraccion,
        List<UnidadVectorizada> Unidades,
        DateOnly? FechaAprobacion,
        int? AnosTranscurridos,
        int? NumeroMatricula);

    private sealed record DestinoVectorizado(DestinoAnalisisRequest Solicitud, List<UnidadVectorizada> Unidades);
}
