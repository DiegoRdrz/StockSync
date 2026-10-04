using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Application.Stocks;

public sealed record StockResponse(
    Guid Id,
    Guid ProductoId,
    Guid SucursalId,
    int Cantidad)
{
    public static StockResponse Desde(Stock stock) => new(
        stock.Id,
        stock.ProductoId,
        stock.SucursalId,
        stock.Cantidad);
}
