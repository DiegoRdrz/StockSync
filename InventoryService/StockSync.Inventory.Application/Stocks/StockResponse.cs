using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Application.Stocks;

public sealed record StockResponse(
    Guid Id,
    Guid ProductId,
    Guid BranchId,
    int Quantity)
{
    public static StockResponse From(Stock stock) => new(
        stock.Id,
        stock.ProductId,
        stock.BranchId,
        stock.Quantity);
}
