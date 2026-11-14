namespace StockSync.Inventory.Application.Stocks;

// The product and branch assignment never changes; only the quantity can change.
public sealed record StockQuantityRequest(int NewQuantity);
