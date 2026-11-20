using Microsoft.AspNetCore.Mvc;
using StockSync.Reporting.Application;
using StockSync.Reporting.Application.Common;

namespace StockSync.Reporting.WebApi.Controllers;

[ApiController]
[Route("api/reports/sales")]
public sealed class SalesReportsController(ISalesReports reports, TimeProvider clock) : ControllerBase
{
    [HttpGet("summary")]
    public Task<SalesSummary> Summary([FromQuery] ReportQuery query, CancellationToken token) =>
        reports.SummaryAsync(query.ToFilter(Request.Query, clock), token);

    [HttpGet("daily")]
    public Task<List<DailySales>> Daily([FromQuery] ReportQuery query, CancellationToken token) =>
        reports.DailyAsync(query.ToFilter(Request.Query, clock), token);

    [HttpGet("top-products")]
    public Task<List<ProductSales>> Top([FromQuery] ReportQuery query, CancellationToken token, [FromQuery] int limit = 10) =>
        reports.TopAsync(query.ToFilter(Request.Query, clock), limit, token);

    [HttpGet]
    public Task<PagedResult<SaleDetail>> Detail([FromQuery] ReportQuery query, CancellationToken token,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        reports.DetailAsync(query.ToFilter(Request.Query, clock), page, pageSize, token);
}
