using Microsoft.AspNetCore.Mvc;
using StockSync.Reporting.Application;
using StockSync.Reporting.Application.Common;

namespace StockSync.Reporting.WebApi.Controllers;

public sealed class ReportQuery
{
    public string? From { get; init; }
    public string? To { get; init; }
    public Guid? BranchId { get; init; }
}

[ApiController]
[Route("api/reports/sales")]
public sealed class SalesReportsController(ISalesReports reports, TimeProvider clock) : ControllerBase
{
    private SalesFilter Filter(ReportQuery query) => SalesFilter.Create(query.From, query.To, query.BranchId,
        clock.GetUtcNow().UtcDateTime);

    [HttpGet("summary")]
    public Task<SalesSummary> Summary([FromQuery] ReportQuery query, CancellationToken token) =>
        reports.SummaryAsync(Filter(query), token);

    [HttpGet("daily")]
    public Task<List<DailySales>> Daily([FromQuery] ReportQuery query, CancellationToken token) =>
        reports.DailyAsync(Filter(query), token);

    [HttpGet("top-products")]
    public Task<List<ProductSales>> Top([FromQuery] ReportQuery query, CancellationToken token, [FromQuery] int limit = 10) =>
        reports.TopAsync(Filter(query), limit, token);

    [HttpGet]
    public Task<PagedResult<SaleDetail>> Detail([FromQuery] ReportQuery query, CancellationToken token,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        reports.DetailAsync(Filter(query), page, pageSize, token);
}
