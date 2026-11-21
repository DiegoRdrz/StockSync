using Microsoft.EntityFrameworkCore;
using StockSync.Reporting.Application;
using StockSync.Reporting.Application.Common;
using StockSync.Reporting.Domain;

namespace StockSync.Reporting.Infrastructure;

public sealed class SalesReports(ReportingDbContext db) : ISalesReports
{
    private IQueryable<Sale> Filter(SalesFilter filter)
    {
        var sales = db.Sales.AsNoTracking().Where(s => s.ConfirmedAt >= filter.From && s.ConfirmedAt <= filter.To);
        return filter.BranchId is Guid branch ? sales.Where(s => s.BranchId == branch) : sales;
    }

    public async Task<SalesSummary> SummaryAsync(SalesFilter filter, CancellationToken token) =>
        await Filter(filter).GroupBy(s => 1)
            .Select(g => new SalesSummary(g.Sum(s => s.Total), g.Count(), g.Average(s => s.Total), g.Sum(s => s.Units)))
            .SingleOrDefaultAsync(token) ?? new SalesSummary(0, 0, 0, 0);

    public Task<List<DailySales>> DailyAsync(SalesFilter filter, CancellationToken token) =>
        Filter(filter).GroupBy(s => s.ConfirmedAt.Date).OrderBy(g => g.Key)
            .Select(g => new DailySales(g.Key, g.Sum(s => s.Total), g.Count(), g.Sum(s => s.Units))).ToListAsync(token);

    public Task<List<ProductSales>> TopAsync(SalesFilter filter, int limit, CancellationToken token)
    {
        SalesFilter.ValidateLimit(limit);
        var lines = from sale in Filter(filter)
                    from line in sale.Lines
                    select new { sale.ConfirmedAt, sale.OrderId, Line = line };
        return lines.GroupBy(x => x.Line.ProductId)
            .OrderByDescending(g => g.Sum(x => (long)x.Line.Quantity))
            .ThenByDescending(g => g.Sum(x => x.Line.LineTotal)).ThenBy(g => g.Key)
            .Select(g => new ProductSales(g.Key,
                g.OrderByDescending(x => x.ConfirmedAt).ThenBy(x => x.OrderId).Select(x => x.Line.ProductSku).First(),
                g.OrderByDescending(x => x.ConfirmedAt).ThenBy(x => x.OrderId).Select(x => x.Line.ProductName).First(),
                g.Sum(x => (long)x.Line.Quantity), g.Sum(x => x.Line.LineTotal)))
            .Take(limit).ToListAsync(token);
    }

    public async Task<PagedResult<SaleDetail>> DetailAsync(SalesFilter filter, int page, int pageSize, CancellationToken token)
    {
        SalesFilter.ValidatePage(page, pageSize);
        var sales = Filter(filter);
        var total = await sales.CountAsync(token);
        var offset = (long)(page - 1) * pageSize;
        if (offset >= total) return new([], page, pageSize, total);
        var rows = await sales.OrderByDescending(s => s.ConfirmedAt).ThenBy(s => s.OrderId)
            .Skip((int)offset).Take(pageSize)
            .Select(s => new SaleDetail(s.OrderId, s.BranchId, s.ConfirmedAt, s.Total,
                s.Lines.OrderBy(l => l.LineNumber).ToList())).ToListAsync(token);
        return new(rows, page, pageSize, total);
    }
}
