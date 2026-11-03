using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Siha.Contracts;
using Siha.MsC.Api.Services;

namespace Siha.MsC.Api.Controllers;

/// <summary>Unidad de un plan analítico que se envía para precargar su embedding.</summary>
public sealed record UnidadPlanRequest(int Numero, string Contenidos);

/// <summary>Plan analítico (asignatura con sus unidades) que se envía para precargar embeddings.</summary>
public sealed record PlanEmbeddingsRequest(string CodigoAsignatura, IReadOnlyCollection<UnidadPlanRequest> Unidades);

/// <summary>
/// Precarga de la caché de embeddings EMBEDDING_UNIDAD (S2-S05 / US-10). El administrador envía los planes
/// analíticos del catálogo (los 43 de Software) y C guarda un vector por unidad para no recalcularlo
/// en cada análisis. Va bajo /api/extracciones para usar la ruta del Gateway que ya llega a C.
/// </summary>
[ApiController]
[Route("api/extracciones/embeddings")]
[Authorize(Roles = RolesSistema.Administrador)]
public class EmbeddingsController : ControllerBase
{
    private readonly IEmbeddingCacheService _cache;

    public EmbeddingsController(IEmbeddingCacheService cache) => _cache = cache;

    [HttpPost("precalcular")]
    [ProducesResponseType(typeof(ResultadoPrecalculoEmbeddings), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ResultadoPrecalculoEmbeddings>> Precalcular(
        [FromBody] IReadOnlyCollection<PlanEmbeddingsRequest> planes, CancellationToken cancellationToken)
    {
        if (planes is null || planes.Count == 0)
            return BadRequest(new { mensaje = "Debe enviar al menos un plan analítico." });
        if (planes.Any(p => string.IsNullOrWhiteSpace(p.CodigoAsignatura) || p.Unidades is null))
            return BadRequest(new { mensaje = "Cada plan necesita código de asignatura y unidades." });

        var unidades = planes.SelectMany(p => p.Unidades.Select(u =>
            new UnidadPlanEmbedding(p.CodigoAsignatura, u.Numero, u.Contenidos)));

        var resultado = await _cache.PrecalcularAsync(unidades, cancellationToken);
        return Ok(resultado);
    }
}
