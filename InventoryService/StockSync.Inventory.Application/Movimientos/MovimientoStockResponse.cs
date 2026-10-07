using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Application.Movimientos;

public sealed record MovimientoStockResponse(
    Guid Id, Guid StockId, string Tipo, int Cantidad,
    int CantidadAnterior, int CantidadPosterior, DateTime Fecha)
{
    public static MovimientoStockResponse Desde(MovimientoStock movimiento) => new(
        movimiento.Id, movimiento.StockId, movimiento.Tipo.ToString(), movimiento.Cantidad,
        movimiento.CantidadAnterior, movimiento.CantidadPosterior, movimiento.Fecha);
}
