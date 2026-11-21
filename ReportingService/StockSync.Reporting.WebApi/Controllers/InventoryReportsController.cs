using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockSync.Reporting.Application;
using StockSync.Reporting.Application.Common;

namespace StockSync.Reporting.WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/reports/inventory")]
public sealed class InventoryReportsController(IInventoryReports reports, TimeProvider clock) : ControllerBase
{
    [HttpGet("movements/summary")]
    public Task<MovementSummary> Movements([FromQuery] ReportQuery query, CancellationToken token) =>
        reports.MovementsAsync(query.ToFilter(Request.Query, clock), token);

    [HttpGet("low-stock")]
    public Task<PagedResult<LowStock>> LowStock([FromQuery] Guid? branchId, CancellationToken token,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        ReportQuery.ValidateValues(Request.Query, "branchId", "page", "pageSize");
        return reports.LowStockAsync(branchId, page, pageSize, token);
    }
}
