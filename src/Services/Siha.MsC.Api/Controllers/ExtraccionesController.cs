using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Siha.Contracts;
using Siha.MsC.Api.Contracts.Dtos;
using Siha.MsC.Api.Domain;
using Siha.MsC.Api.Domain.Entities;
using Siha.MsC.Api.Services;

namespace Siha.MsC.Api.Controllers;

/// <summary>
/// Consulta y confirmación humana de los datos extraídos de un PDF (US-07, S2-J02).
/// El operador revisa créditos, horas y unidades; la evaluación no avanza hasta confirmarlos.
/// También expone el cálculo de proporción de créditos y controles normativos (US-09, S2-J04).
/// Y la evaluación de similitud semántica de contenidos (US-10, S2-J05/J06).
/// Y la búsqueda de precedentes homologatorios (US-11, S2-J08).
/// Y el análisis completo orquestado (US-11, S2-J09).
/// </summary>
[ApiController]
[Route("api/extracciones")]
public class ExtraccionesController : ControllerBase
{
    private readonly ExtraccionService _servicio;
    private readonly IReglaCreditosStrategy _reglaCreditos;
    private readonly IReglaContenidoStrategy _reglaContenido;
    private readonly IPrecedenteService _precedenteService;
    private readonly IAgenteAnalizadorService _agenteAnalizador;

    public ExtraccionesController(
        ExtraccionService servicio,
        IReglaCreditosStrategy reglaCreditos,
        IReglaContenidoStrategy reglaContenido,
        IPrecedenteService precedenteService,
        IAgenteAnalizadorService agenteAnalizador)
    {
        _servicio = servicio;
        _reglaCreditos = reglaCreditos;
        _reglaContenido = reglaContenido;
        _precedenteService = precedenteService;
        _agenteAnalizador = agenteAnalizador;
    }

    /// <summary>
    /// Bandeja de procesamiento (S2-A01, US-07): extracciones recientes con su confianza y estado.
    /// La ruta es <c>/api/extracciones/bandeja</c> para que el Gateway la encamine con
    /// <c>/api/extracciones/{everything}</c>.
    /// </summary>
    [HttpGet("bandeja")]
    [Authorize(Roles = $"{RolesSistema.Operador},{RolesSistema.Administrador},{RolesSistema.Docente}")]
    [ProducesResponseType(typeof(IReadOnlyCollection<ExtraccionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyCollection<ExtraccionResponse>>> Bandeja(
        [FromQuery] string? estado,
        [FromQuery] Guid? expedienteId,
        CancellationToken cancellationToken)
    {
        var extracciones = await _servicio.ListarAsync(estado, expedienteId, cancellationToken);
        return Ok(extracciones.Select(ARespuesta).ToList());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = $"{RolesSistema.Operador},{RolesSistema.Administrador},{RolesSistema.Docente}")]
    [ProducesResponseType(typeof(ExtraccionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExtraccionResponse>> Obtener(Guid id, CancellationToken cancellationToken)
    {
        var extraccion = await _servicio.ObtenerAsync(id, cancellationToken);

        return extraccion is null
            ? NotFound(new { mensaje = $"La extracción {id} no existe." })
            : Ok(ARespuesta(extraccion));
    }

    /// <summary>
    /// Confirma o corrige la extracción. Solo el OPERADOR (o ADMINISTRADOR) puede validar los
    /// datos; una extracción ya confirmada o pendiente de OCR/LLM devuelve 409.
    /// </summary>
    [HttpPut("{id:guid}/confirmar")]
    [Authorize(Roles = $"{RolesSistema.Operador},{RolesSistema.Administrador}")]
    [ProducesResponseType(typeof(ExtraccionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ExtraccionResponse>> Confirmar(
        Guid id,
        [FromBody] ConfirmarExtraccionRequest? request,
        CancellationToken cancellationToken)
    {
        var usuario = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(usuario))
        {
            usuario = User.FindFirstValue(ClaimTypes.Email)
                ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? "desconocido";
        }

        var resultado = await _servicio.ConfirmarAsync(id, request, usuario, cancellationToken);

        return resultado.Resultado switch
        {
            ResultadoConfirmacionExtraccion.Confirmada => Ok(ARespuesta(resultado.Extraccion!)),
            ResultadoConfirmacionExtraccion.NoEncontrada =>
                NotFound(new { mensaje = $"La extracción {id} no existe." }),
            ResultadoConfirmacionExtraccion.DatosInvalidos => BadRequest(new
            {
                mensaje = "Los datos de confirmación no son válidos.",
                errores = resultado.Errores
            }),
            ResultadoConfirmacionExtraccion.YaConfirmada => Conflict(new
            {
                mensaje = "La extracción ya fue confirmada.",
                estado = resultado.Extraccion!.Estado,
                confirmadoPor = resultado.Extraccion.ConfirmadoPor
            }),
            ResultadoConfirmacionExtraccion.PendienteOcrLlm => Conflict(new
            {
                mensaje = "La extracción está pendiente de OCR/LLM; complete el flujo US-10 antes de confirmarla.",
                estado = resultado.Extraccion!.Estado
            }),
            ResultadoConfirmacionExtraccion.Cancelada => Conflict(new
            {
                mensaje = "La extracción fue cancelada: el PDF no tiene capa de texto y el análisis OCR/LLM no está disponible. Cargue un PDF con texto seleccionable.",
                estado = resultado.Extraccion!.Estado,
                observacion = resultado.Extraccion.Observacion
            }),
            _ => Conflict(new
            {
                mensaje = "La extracción no está en estado PENDIENTE_CONFIRMACION.",
                estado = resultado.Extraccion!.Estado
            })
        };
    }

    /// <summary>
    /// Calcula la proporción de créditos (Pcred) y evalúa controles normativos (US-09, S2-J04).
    /// <para>Pcred = min(100, (credOrigen / credDestino) * 100). Solo dato de apoyo.</para>
    /// <para>RN-03 (CAU-P-011): >10 años → VALIDACION_CONOCIMIENTOS.</para>
    /// <para>RN-16: 3ra matrícula destino → BLOQUEO_3RA_MATRICULA (bloqueo duro).</para>
    /// <para>RN-15: Créditos externos → sin 48h/crédito automático.</para>
    /// </summary>
    [HttpPost("calcular-creditos")]
    [Authorize(Roles = $"{RolesSistema.Operador},{RolesSistema.Administrador},{RolesSistema.Docente}")]
    [ProducesResponseType(typeof(EvaluacionCreditosResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult<EvaluacionCreditosResponse> CalcularCreditos(
        [FromBody] CalcularCreditosRequest request)
    {
        try
        {
            var parametros = new ParametrosCreditos(
                CreditosOrigen: request.CreditosOrigen,
                CreditosDestino: request.CreditosDestino,
                AnosTranscurridos: request.AnosTranscurridos,
                NumeroMatriculaDestino: request.NumeroMatriculaDestino,
                EsCreditoExterno: request.EsCreditoExterno,
                RutaNormativa: request.RutaNormativa);

            var resultado = _reglaCreditos.Calcular(parametros);

            return Ok(new EvaluacionCreditosResponse(
                resultado.Pcred,
                resultado.Categoria,
                resultado.Alertas,
                resultado.BloqueoDuro,
                resultado.DetalleCalculo));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensaje = "Parámetros inválidos.", error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensaje = "Error de cálculo.", error = ex.Message });
        }
    }

    /// <summary>
    /// Calcula la cobertura semántica de contenidos (Pcont) mediante similitud coseno de embeddings (US-10, S2-J06/J07).
    /// <para>sj = max_u cos(t_j, u); cubierta si sj ≥ τ (ej. 75%).</para>
    /// <para>Pcont = Σ(h_j * covered_j) / Σ(h_j) * 100  — ponderado por horas.</para>
    /// <para>N:1: varias unidades origen → una destino (se toma max similitud).</para>
    /// <para>RN-19: no reutilizar materia origen en dos destinos del mismo expediente.</para>
    /// </summary>
    [HttpPost("calcular-contenido")]
    [Authorize(Roles = $"{RolesSistema.Operador},{RolesSistema.Administrador},{RolesSistema.Docente}")]
    [ProducesResponseType(typeof(EvaluacionContenidoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult<EvaluacionContenidoResponse> CalcularContenido(
        [FromBody] CalcularContenidoRequest request)
    {
        try
        {
            var unidadesOrigen = request.UnidadesOrigen
                .Select(u => new UnidadOrigenEmbedding(
                    u.Numero, u.Nombre, u.Horas, u.Contenidos, u.Embedding.ToArray()))
                .ToList();

            var unidadesDestino = request.UnidadesDestino
                .Select(u => new UnidadDestinoEmbedding(
                    u.Numero, u.Nombre, u.Horas, u.Contenidos, u.Embedding.ToArray()))
                .ToList();

            var parametros = new ParametrosContenido(
                UnidadesOrigen: unidadesOrigen,
                UnidadesDestino: unidadesDestino,
                Tau: request.Tau,
                RutaNormativa: request.RutaNormativa,
                ValidarRN19: request.ValidarRN19);

            var resultado = _reglaContenido.Calcular(parametros);

            return Ok(new EvaluacionContenidoResponse(
                resultado.Pcont,
                resultado.UnidadesCubiertas.Select(u => new UnidadCoberturaResponse(
                    u.Numero, u.Nombre, u.Horas, u.SimilitudMaxima, u.Cubierta, u.UnidadesOrigenContribuyentes)).ToList(),
                resultado.UnidadesFaltantes.Select(u => new UnidadCoberturaResponse(
                    u.Numero, u.Nombre, u.Horas, u.SimilitudMaxima, u.Cubierta, u.UnidadesOrigenContribuyentes)).ToList(),
                resultado.TauUsado,
                resultado.DetalleCalculo,
                resultado.ViolacionRN19,
                resultado.UnidadesOrigenDuplicadas));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensaje = "Parámetros inválidos.", error = ex.Message });
        }
    }

    private static ExtraccionResponse ARespuesta(Extraccion extraccion) => new(
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

    /// <summary>
    /// Busca precedentes homologatorios por coincidencia EXACTA (US-11, S2-J08).
    /// <para>Match exacto: mismas asignaturas origen (conjunto), misma asignatura destino, misma institución.</para>
    /// <para>Sin coincidencia → lista vacía []. NO inventa "parecidos".</para>
    /// </summary>
    [HttpPost("precedentes/buscar")]
    [Authorize(Roles = $"{RolesSistema.Operador},{RolesSistema.Administrador},{RolesSistema.Docente}")]
    [ProducesResponseType(typeof(IReadOnlyCollection<PrecedenteResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyCollection<PrecedenteResponse>>> BuscarPrecedentes(
        [FromBody] BuscarPrecedenteRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var resultados = await _precedenteService.BuscarAsync(request, cancellationToken);
            return Ok(resultados);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensaje = "Parámetros inválidos.", error = ex.Message });
        }
    }

    /// <summary>
    /// Ejecuta el análisis completo orquestado (US-11, S2-J09).
    /// <para>Secuencia: 1) Extracción confirmada → 2) gRPC reglas (θ, δ, τ) → 3) ReglaCreditosStrategy (Pcred + RN-03/15/16) → 4) ReglaContenidoStrategy (Pcont + RN-19) → 5) Búsqueda precedentes.</para>
    /// <para>Devuelve resultado consolidado con estado final para el docente.</para>
    /// </summary>
    [HttpPost("{id:guid}/analizar")]
    [Authorize(Roles = $"{RolesSistema.Operador},{RolesSistema.Administrador},{RolesSistema.Docente}")]
    [ProducesResponseType(typeof(AnalisisCompletoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnalisisCompletoResponse>> AnalizarCompleto(
        Guid id,
        [FromBody] AnalizarExtraccionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var parametros = new ParametrosAnalisisCompleto(
                AnosTranscurridos: request.AnosTranscurridos,
                NumeroMatriculaDestino: request.NumeroMatriculaDestino,
                EsCreditoExterno: request.EsCreditoExterno,
                RutaNormativa: request.RutaNormativa,
                CodigoAsignaturaDestino: request.CodigoAsignaturaDestino,
                InstitucionOrigen: request.InstitucionOrigen,
                CreditosDestino: request.CreditosDestino,
                UnidadesDestino: request.UnidadesDestino,
                ValidarRN19: request.ValidarRN19);

            var resultado = await _agenteAnalizador.AnalizarAsync(id, parametros, cancellationToken);
            return Ok(new AnalisisCompletoResponse(
                resultado.Extraccion,
                resultado.Creditos,
                resultado.Contenido,
                resultado.Precedentes,
                resultado.EstadoFinal,
                resultado.AlertasGlobales,
                resultado.DetalleTrazabilidad));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensaje = "Parámetros inválidos.", error = ex.Message });
        }
    }
}
