namespace StockSync.Inventory.Application.Stocks;

public sealed record StockRequest(Guid ProductId, Guid BranchId, int Quantity);
