using StockSync.Reporting.Application.Common;

namespace StockSync.Reporting.Application;

public sealed record MovementTypeTotals(string Type, int Count, long Quantity, long NetChange);
public sealed record MovementDayTotals(DateTime Day, int Count, long Quantity, long NetChange);
public sealed record MovementProductTotals(Guid ProductId, int Count, long Quantity, long NetChange);
public sealed record MovementSummary(List<MovementTypeTotals> ByType, List<MovementDayTotals> ByDay,
    List<MovementProductTotals> ByProduct);
public sealed record LowStock(Guid StockId, Guid ProductId, string ProductName, string ProductSku, Guid BranchId,
    int Quantity, int MinimumStock, int Shortage);

public interface IInventoryReports
{
    Task<MovementSummary> MovementsAsync(SalesFilter filter, CancellationToken token);
    Task<PagedResult<LowStock>> LowStockAsync(Guid? branchId, int page, int pageSize, CancellationToken token);
}
