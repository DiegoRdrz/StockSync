using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockSync.Identity.Application.Tenants;
using StockSync.Identity.Application.Tenants.DTOs;
using StockSync.Identity.Domain.Entities;

namespace StockSync.Identity.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/tenants")]
public class TenantsController : ControllerBase
{
    private readonly ITenantService _tenantService;

    public TenantsController(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    [HttpGet("current")]
    [ProducesResponseType(typeof(TenantResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentTenant(CancellationToken cancellationToken)
    {
        var response = await _tenantService.GetCurrentTenantAsync(cancellationToken);
        return Ok(response);
    }

    [HttpPut("current")]
    [Authorize(Roles = nameof(RoleType.Owner))]
    [ProducesResponseType(typeof(TenantResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateCurrentTenant([FromBody] UpdateTenantRequest request, CancellationToken cancellationToken)
    {
        var response = await _tenantService.UpdateCurrentTenantAsync(request, cancellationToken);
        return Ok(response);
    }
}

