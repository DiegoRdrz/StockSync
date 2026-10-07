using StockSync.Inventory.Domain.Repositories;

namespace StockSync.Inventory.Application.Stocks;

public sealed record StockBajoMinimoResponse(
    Guid StockId,
    Guid ProductoId,
    string ProductoNombre,
    string ProductoSku,
    Guid SucursalId,
    int Cantidad,
    int StockMinimo,
    int Faltante)
{
    public static StockBajoMinimoResponse Desde(StockBajoMinimoDetalle detalle) => new(
        detalle.Stock.Id,
        detalle.Stock.ProductoId,
        detalle.ProductoNombre,
        detalle.ProductoSku,
        detalle.Stock.SucursalId,
        detalle.Stock.Cantidad,
        detalle.StockMinimo,
        detalle.StockMinimo - detalle.Stock.Cantidad);
}
