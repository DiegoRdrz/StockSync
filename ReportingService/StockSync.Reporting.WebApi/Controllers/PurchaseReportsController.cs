using Microsoft.AspNetCore.Mvc;
using StockSync.Reporting.Application;

namespace StockSync.Reporting.WebApi.Controllers;

[ApiController]
[Route("api/reports/purchases")]
public sealed class PurchaseReportsController(IPurchaseReports reports, TimeProvider clock) : ControllerBase
{
    [HttpGet("summary")]
    public Task<PurchaseSummary> Summary([FromQuery] ReportQuery query, CancellationToken token) =>
        reports.SummaryAsync(query.ToFilter(Request.Query, clock), token);

    [HttpGet("daily")]
    public Task<List<DailyPurchases>> Daily([FromQuery] ReportQuery query, CancellationToken token) =>
        reports.DailyAsync(query.ToFilter(Request.Query, clock), token);
}
