namespace StockSync.Inventory.Application.Stocks;

public sealed record StockRequest(Guid ProductoId, Guid SucursalId, int Cantidad);
