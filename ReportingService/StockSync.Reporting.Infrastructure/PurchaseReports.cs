using Microsoft.EntityFrameworkCore;
using StockSync.Reporting.Application;
using StockSync.Reporting.Domain;

namespace StockSync.Reporting.Infrastructure;

public sealed class PurchaseReports(ReportingDbContext db) : IPurchaseReports
{
    private IQueryable<Purchase> Filter(SalesFilter filter)
    {
        var purchases = db.Purchases.AsNoTracking().Where(p => p.ReceivedAt >= filter.From && p.ReceivedAt <= filter.To);
        return filter.BranchId is Guid branch ? purchases.Where(p => p.BranchId == branch) : purchases;
    }

    public async Task<PurchaseSummary> SummaryAsync(SalesFilter filter, CancellationToken token) =>
        await Filter(filter).GroupBy(p => 1)
            .Select(g => new PurchaseSummary(g.Sum(p => p.Total), g.Count(), g.Sum(p => p.Units)))
            .SingleOrDefaultAsync(token) ?? new PurchaseSummary(0, 0, 0);

    public Task<List<DailyPurchases>> DailyAsync(SalesFilter filter, CancellationToken token) =>
        Filter(filter).GroupBy(p => p.ReceivedAt.Date).OrderBy(g => g.Key)
            .Select(g => new DailyPurchases(g.Key, g.Sum(p => p.Total), g.Count(), g.Sum(p => p.Units)))
            .ToListAsync(token);
}
