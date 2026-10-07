using StockSync.Inventory.Domain.Repositories;

namespace StockSync.Inventory.Application.Movimientos;

public sealed record MovimientoStockListadoResponse(
    Guid Id, Guid StockId, Guid ProductoId, string ProductoNombre, string ProductoSku,
    Guid SucursalId, string Tipo, int Cantidad, int CantidadAnterior, int CantidadPosterior, DateTime Fecha)
{
    public static MovimientoStockListadoResponse Desde(MovimientoStockDetalle detalle) => new(
        detalle.Movimiento.Id, detalle.Movimiento.StockId, detalle.ProductoId,
        detalle.ProductoNombre, detalle.ProductoSku, detalle.SucursalId,
        detalle.Movimiento.Tipo.ToString(), detalle.Movimiento.Cantidad,
        detalle.Movimiento.CantidadAnterior, detalle.Movimiento.CantidadPosterior, detalle.Movimiento.Fecha);
}
