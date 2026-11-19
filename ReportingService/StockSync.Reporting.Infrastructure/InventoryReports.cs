using System.Data;
using Microsoft.EntityFrameworkCore;
using StockSync.Reporting.Application;
using StockSync.Reporting.Application.Common;

namespace StockSync.Reporting.Infrastructure;

public sealed class InventoryReports(ReportingDbContext db) : IInventoryReports
{
    public async Task<MovementSummary> MovementsAsync(SalesFilter filter, CancellationToken token)
    {
        var movements = db.StockMovements.AsNoTracking()
            .Where(m => m.MovementDate >= filter.From && m.MovementDate <= filter.To);
        if (filter.BranchId is Guid branch) movements = movements.Where(m => m.BranchId == branch);

        // The three groupings must describe the same history while deliveries are committing.
        await using var snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
        var byType = await movements.GroupBy(m => m.Type).OrderBy(g => g.Key)
            .Select(g => new MovementTypeTotals(g.Key, g.Count(), g.Sum(m => (long)m.Quantity),
                g.Sum(m => (long)m.NewQuantity - m.PreviousQuantity))).ToListAsync(token);
        var byDay = await movements.GroupBy(m => m.MovementDate.Date).OrderBy(g => g.Key)
            .Select(g => new MovementDayTotals(g.Key, g.Count(), g.Sum(m => (long)m.Quantity),
                g.Sum(m => (long)m.NewQuantity - m.PreviousQuantity))).ToListAsync(token);
        var byProduct = await movements.GroupBy(m => m.ProductId).OrderBy(g => g.Key)
            .Select(g => new MovementProductTotals(g.Key, g.Count(), g.Sum(m => (long)m.Quantity),
                g.Sum(m => (long)m.NewQuantity - m.PreviousQuantity))).ToListAsync(token);
        await snapshot.CommitAsync(token);
        return new(byType, byDay, byProduct);
    }

    public async Task<PagedResult<LowStock>> LowStockAsync(Guid? branchId, int page, int pageSize, CancellationToken token)
    {
        SalesFilter.ValidatePage(page, pageSize);
        SalesFilter.ValidateBranch(branchId);
        var levels = db.StockLevels.AsNoTracking().Where(l => l.MinimumStock > 0 && l.Quantity < l.MinimumStock);
        if (branchId is Guid branch) levels = levels.Where(l => l.BranchId == branch);

        var total = await levels.CountAsync(token);
        var offset = (long)(page - 1) * pageSize;
        if (offset >= total) return new([], page, pageSize, total);
        var rows = await levels.OrderByDescending(l => l.MinimumStock - l.Quantity).ThenBy(l => l.StockId)
            .Skip((int)offset).Take(pageSize)
            .Select(l => new LowStock(l.StockId, l.ProductId, l.ProductName, l.ProductSku, l.BranchId,
                l.Quantity, l.MinimumStock, l.MinimumStock - l.Quantity)).ToListAsync(token);
        return new(rows, page, pageSize, total);
    }
}
