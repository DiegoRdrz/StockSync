using StockSync.Inventory.Domain.Repositories;

namespace StockSync.Inventory.Application.Stocks;

public sealed record LowStockResponse(
    Guid StockId,
    Guid ProductId,
    string ProductName,
    string ProductSku,
    Guid BranchId,
    int Quantity,
    int MinimumStock,
    int Shortage)
{
    public static LowStockResponse From(LowStockDetail detail) => new(
        detail.Stock.Id,
        detail.Stock.ProductId,
        detail.ProductName,
        detail.ProductSku,
        detail.Stock.BranchId,
        detail.Stock.Quantity,
        detail.MinimumStock,
        detail.MinimumStock - detail.Stock.Quantity);
}
