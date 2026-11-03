using System.Text.Json;
using Siha.MsA.Api.Grpc;
using Siha.MsC.Api.Contracts.Dtos;
using Siha.MsC.Api.Data;
using Siha.MsC.Api.Domain;
using Siha.MsC.Api.Domain.Entities;
using Siha.MsC.Api.Grpc;
using Siha.MsC.Api.Options;

// Usar el enum del DTO (contrato público) para evitar duplicación
using EstadoEvaluacionFinal = Siha.MsC.Api.Contracts.Dtos.EstadoEvaluacionFinal;

namespace Siha.MsC.Api.Services;

/// <summary>
/// Orquestador del agente analizador: encadena extracción → gRPC reglas → créditos → contenidos → precedentes.
///
/// Si gRPC falla → estado ErrorReglasGrpc (no inventa valores).
/// Si extracción no confirmada → estado ExtraccionNoConfirmada.
/// Controles normativos (RN-03, RN-16, RN-15) se evalúan y reportan.
/// RN-19 se evalúa en contenidos.
/// Precedentes: coincidencia exacta o [].
/// </summary>
public sealed class AgenteAnalizadorService : IAgenteAnalizadorService
{
    private readonly SihaDbContextC _db;
    private readonly ExtraccionService _extraccionService;
    private readonly IReglasGrpcClient _reglasGrpcClient;
    private readonly IReglaCreditosStrategy _reglaCreditos;
    private readonly IReglaContenidoStrategy _reglaContenido;
    private readonly IPrecedenteService _precedenteService;
    private readonly IOllamaEmbeddingService _ollamaEmbeddings;
    private readonly ILogger<AgenteAnalizadorService> _logger;
    private readonly IEmbeddingCacheService? _cacheEmbeddings;

    public AgenteAnalizadorService(
        SihaDbContextC db,
        ExtraccionService extraccionService,
        IReglasGrpcClient reglasGrpcClient,
        IReglaCreditosStrategy reglaCreditos,
        IReglaContenidoStrategy reglaContenido,
        IPrecedenteService precedenteService,
        IOllamaEmbeddingService ollamaEmbeddings,
        ILogger<AgenteAnalizadorService> logger,
        IEmbeddingCacheService? cacheEmbeddings = null)
    {
        _db = db;
        _extraccionService = extraccionService;
        _reglasGrpcClient = reglasGrpcClient;
        _reglaCreditos = reglaCreditos;
        _reglaContenido = reglaContenido;
        _precedenteService = precedenteService;
        _ollamaEmbeddings = ollamaEmbeddings;
        _logger = logger;
        _cacheEmbeddings = cacheEmbeddings;
    }

    public async Task<ResultadoAnalisisCompleto> AnalizarAsync(
        Guid extraccionId,
        ParametrosAnalisisCompleto parametros,
        CancellationToken cancellationToken = default)
    {
        var alertas = new List<string>();
        var trazabilidad = new List<string>();

        // 1) Obtener extracción confirmada
        var extraccion = await _extraccionService.ObtenerAsync(extraccionId, cancellationToken);
        if (extraccion is null)
        {
            return CrearResultadoError(extraccionId, EstadoEvaluacionFinal.ExtraccionNoConfirmada,
                "La extracción no existe.", alertas, trazabilidad);
        }

        if (extraccion.Estado != EstadosExtraccion.Confirmada)
        {
            return CrearResultadoError(extraccionId, EstadoEvaluacionFinal.ExtraccionNoConfirmada,
                $"La extracción está en estado {extraccion.Estado}, debe estar CONFIRMADA.", alertas, trazabilidad);
        }

        trazabilidad.Add($"Extracción {extraccionId} confirmada por {extraccion.ConfirmadoPor} el {extraccion.ConfirmadoUtc:O}");

        // 2) gRPC ReglasGrpc: obtener θ, δ, τ y reglas normativas
        ConfiguracionRuta? configReglas = null;
        try
        {
            configReglas = await _reglasGrpcClient.ObtenerConfiguracionAsync(
                parametros.RutaNormativa,
                cancellationToken: cancellationToken);

            if (configReglas?.ParametrosConfigurados == true)
            {
                trazabilidad.Add($"gRPC Reglas OK: θ={configReglas.Parametros.Theta} δ={configReglas.Parametros.Delta} τ={configReglas.Parametros.Tau} v{configReglas.Parametros.Version} (Ruta={parametros.RutaNormativa})");
            }
            else
            {
                alertas.Add($"No hay parámetros θ, δ, τ configurados para ruta {parametros.RutaNormativa}. Se usará τ=75% por defecto.");
                trazabilidad.Add($"gRPC Reglas: sin parámetros configurados para {parametros.RutaNormativa}");
            }
        }
        catch (ReglasGrpcException ex)
        {
            _logger.LogError(ex, "Error gRPC consultando reglas para extracción {ExtraccionId}", extraccionId);
            return CrearResultadoError(extraccionId, EstadoEvaluacionFinal.ErrorReglasGrpc,
                $"No se pudo obtener configuración de reglas: {ex.Message}", alertas, trazabilidad);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado gRPC reglas para extracción {ExtraccionId}", extraccionId);
            return CrearResultadoError(extraccionId, EstadoEvaluacionFinal.ErrorReglasGrpc,
                "Error de comunicación con el servicio de reglas.", alertas, trazabilidad);
        }

        // 3) ReglaCreditosStrategy: Pcred + controles normativos
        var parametrosCreditos = new ParametrosCreditos(
            CreditosOrigen: extraccion.Creditos ?? 0,
            CreditosDestino: parametros.CreditosDestino,
            AnosTranscurridos: parametros.AnosTranscurridos,
            NumeroMatriculaDestino: parametros.NumeroMatriculaDestino,
            EsCreditoExterno: parametros.EsCreditoExterno,
            RutaNormativa: parametros.RutaNormativa);

        var resultadoCreditos = _reglaCreditos.Calcular(parametrosCreditos);
        var creditosResponse = new EvaluacionCreditosResponse(
            resultadoCreditos.Pcred,
            resultadoCreditos.Categoria,
            resultadoCreditos.Alertas,
            resultadoCreditos.BloqueoDuro,
            resultadoCreditos.DetalleCalculo);

        alertas.AddRange(resultadoCreditos.Alertas);
        trazabilidad.Add($"Creditos: Pcred={resultadoCreditos.Pcred}% Categoría={resultadoCreditos.Categoria} BloqueoDuro={resultadoCreditos.BloqueoDuro}");

        // 4) Generar embeddings para unidades de origen (a partir de sus contenidos)
        var unidadesOrigenConEmbedding = new List<UnidadOrigenEmbedding>();
        foreach (var u in extraccion.Unidades.OrderBy(x => x.Numero))
        {
            var embedding = await _ollamaEmbeddings.GenerarEmbeddingAsync(u.Contenidos, cancellationToken);
            if (embedding != null && embedding.Length > 0)
            {
                unidadesOrigenConEmbedding.Add(new UnidadOrigenEmbedding(
                    u.Numero, u.Nombre, u.Horas, u.Contenidos, embedding));
            }
            else
            {
                alertas.Add($"Unidad origen {u.Numero} ({u.Nombre}): no se pudo generar embedding.");
            }
        }

        if (unidadesOrigenConEmbedding.Count == 0)
        {
            alertas.Add("Ninguna unidad de origen tiene embedding vectorial. No se puede calcular Pcont.");
        }

        // 5) ReglaContenidoStrategy: Pcont + desglose + RN-19
        // S2-A08: la interfaz web envía las unidades del catálogo sin vector. Si una unidad de
        // destino llega sin embedding, se genera aquí con Ollama a partir de sus contenidos.
        var unidadesDestino = new List<UnidadDestinoEmbedding>();
        foreach (var u in parametros.UnidadesDestino)
        {
            var vector = u.Embedding?.ToArray() ?? Array.Empty<float>();
            if (vector.Length == 0)
            {
                var texto = string.IsNullOrWhiteSpace(u.Contenidos) ? u.Nombre : u.Contenidos;
                if (_cacheEmbeddings is not null)
                {
                    vector = await _cacheEmbeddings.ObtenerAsync(
                        parametros.CodigoAsignaturaDestino, u.Numero, texto, cancellationToken)
                        ?? throw new ArgumentException(
                            $"No hay embedding para la unidad destino {u.Numero} ({u.Nombre}): no está en caché y Ollama no respondió.");
                    trazabilidad.Add($"Embedding destino {parametros.CodigoAsignaturaDestino} U{u.Numero} desde caché EMBEDDING_UNIDAD");
                }
                else
                {
                    vector = await _ollamaEmbeddings.GenerarEmbeddingAsync(texto, cancellationToken) ?? Array.Empty<float>();
                }
            }

            if (vector.Length == 0)
            {
                alertas.Add($"Unidad destino {u.Numero} ({u.Nombre}): no se pudo generar embedding.");
                continue;
            }

            unidadesDestino.Add(new UnidadDestinoEmbedding(u.Numero, u.Nombre, u.Horas, u.Contenidos, vector));
        }

        var parametrosContenido = new ParametrosContenido(
            UnidadesOrigen: unidadesOrigenConEmbedding,
            UnidadesDestino: unidadesDestino,
            Tau: configReglas?.Parametros?.Tau ?? 75.0,
            RutaNormativa: parametros.RutaNormativa,
            ValidarRN19: parametros.ValidarRN19);

        var resultadoContenido = _reglaContenido.Calcular(parametrosContenido);
        var contenidoResponse = new EvaluacionContenidoResponse(
            resultadoContenido.Pcont,
            resultadoContenido.UnidadesCubiertas.Select(u => new UnidadCoberturaResponse(
                u.Numero, u.Nombre, u.Horas, u.SimilitudMaxima, u.Cubierta, u.UnidadesOrigenContribuyentes)).ToList(),
            resultadoContenido.UnidadesFaltantes.Select(u => new UnidadCoberturaResponse(
                u.Numero, u.Nombre, u.Horas, u.SimilitudMaxima, u.Cubierta, u.UnidadesOrigenContribuyentes)).ToList(),
            resultadoContenido.TauUsado,
            resultadoContenido.DetalleCalculo,
            resultadoContenido.ViolacionRN19,
            resultadoContenido.UnidadesOrigenDuplicadas);

        alertas.AddRange(resultadoContenido.UnidadesFaltantes
            .Where(u => !u.Cubierta)
            .Select(u => $"Unidad destino {u.Numero} ({u.Nombre}) NO cubierta: similitud {u.SimilitudMaxima:F2}% < τ={resultadoContenido.TauUsado}%"));

        trazabilidad.Add($"Contenido: Pcont={resultadoContenido.Pcont}% τ={resultadoContenido.TauUsado}% Cubiertas={resultadoContenido.UnidadesCubiertas.Count} Faltantes={resultadoContenido.UnidadesFaltantes.Count} RN-19={resultadoContenido.ViolacionRN19}");

        // 6) Búsqueda precedentes (usar el código de asignatura de origen de la extracción)
        var requestPrecedentes = new BuscarPrecedenteRequest(
            new[] { extraccion.CodigoAsignaturaOrigen },
            parametros.CodigoAsignaturaDestino,
            parametros.InstitucionOrigen);

        var precedentes = await _precedenteService.BuscarAsync(requestPrecedentes, cancellationToken);
        trazabilidad.Add($"Precedentes: {precedentes.Count} coincidencia(s) exacta(s) para origen={extraccion.CodigoAsignaturaOrigen} destino={parametros.CodigoAsignaturaDestino} inst={parametros.InstitucionOrigen}");

        // 7) Determinar estado final
        var estadoFinal = DeterminarEstadoFinal(resultadoCreditos, resultadoContenido, extraccion);
        if (estadoFinal != EstadoEvaluacionFinal.ListaParaDictamen)
            alertas.Add($"Estado final: {estadoFinal}");

        // 8) Armar respuesta
        var extraccionDto = new ExtraccionResponse(
            extraccion.Id,
            extraccion.ExpedienteId,
            extraccion.DocumentoId,
            extraccion.CodigoAsignaturaOrigen,
            extraccion.NombreAsignaturaOrigen,
            extraccion.Creditos,
            extraccion.Horas,
            extraccion.Confianza,
            extraccion.Estado,
            extraccion.Estado == EstadosExtraccion.PendienteConfirmacion,
            extraccion.Unidades
                .OrderBy(u => u.Numero)
                .Select(u => new UnidadExtraccionResponse(u.Numero, u.Nombre, u.Horas, u.Contenidos))
                .ToList(),
            extraccion.CreadoUtc,
            extraccion.ConfirmadoUtc,
            extraccion.ConfirmadoPor,
            extraccion.Observacion);

        var detalleTrazabilidad = string.Join(" | ", trazabilidad);

        return new ResultadoAnalisisCompleto(
            Extraccion: extraccionDto,
            Creditos: creditosResponse,
            Contenido: contenidoResponse,
            Precedentes: precedentes,
            EstadoFinal: estadoFinal,
            AlertasGlobales: alertas.Distinct().ToList().AsReadOnly(),
            DetalleTrazabilidad: detalleTrazabilidad);
    }

    private static EstadoEvaluacionFinal DeterminarEstadoFinal(
        EvaluacionCreditos resultadoCreditos,
        EvaluacionContenido resultadoContenido,
        Extraccion extraccion)
    {
        // Bloqueos duros tienen prioridad
        if (resultadoCreditos.BloqueoDuro)
            return EstadoEvaluacionFinal.BloqueoTerceraMatricula;

        if (resultadoContenido.ViolacionRN19)
            return EstadoEvaluacionFinal.BloqueoRN19;

        if (resultadoCreditos.Categoria == "VALIDACION_CONOCIMIENTOS")
            return EstadoEvaluacionFinal.RequiereValidacionConocimientos;

        return EstadoEvaluacionFinal.ListaParaDictamen;
    }

    private static ResultadoAnalisisCompleto CrearResultadoError(
        Guid extraccionId,
        EstadoEvaluacionFinal estado,
        string mensaje,
        List<string> alertas,
        List<string> trazabilidad)
    {
        alertas.Add(mensaje);
        return new ResultadoAnalisisCompleto(
            Extraccion: new ExtraccionResponse(extraccionId, Guid.Empty, null, "", "", null, null, 0, "", true,
                Array.Empty<UnidadExtraccionResponse>(), DateTimeOffset.UtcNow, null, null, null),
            Creditos: new EvaluacionCreditosResponse(0, estado.ToString(), alertas, true, mensaje),
            Contenido: new EvaluacionContenidoResponse(0, Array.Empty<UnidadCoberturaResponse>(),
                Array.Empty<UnidadCoberturaResponse>(), 0, mensaje, false, Array.Empty<int>()),
            Precedentes: Array.Empty<PrecedenteResponse>(),
            EstadoFinal: estado,
            AlertasGlobales: alertas.AsReadOnly(),
            DetalleTrazabilidad: string.Join(" | ", trazabilidad));
    }
}
