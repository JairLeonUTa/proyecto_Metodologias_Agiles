using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Siha.Contracts;
using Siha.MsA.Api.Contracts;
using Siha.MsA.Api.Contracts.Dtos;
using Siha.MsA.Api.Data;

namespace Siha.MsA.Api.Controllers;

/// <summary>Catálogo de roles del sistema (US-01). Consulta restringida a Administrador.</summary>
[ApiController]
[Route("api/roles")]
[Authorize(Roles = RolesSistema.Administrador)]
public class RolesController : ControllerBase
{
    private readonly SihaDbContextA _db;

    public RolesController(SihaDbContextA db)
    {
        _db = db;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<RolDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<RolDto>>> Listar()
    {
        var roles = await _db.Roles.AsNoTracking().OrderBy(r => r.Id).ToListAsync();
        return Ok(roles.Select(Mapeos.ARolDto));
    }
}
