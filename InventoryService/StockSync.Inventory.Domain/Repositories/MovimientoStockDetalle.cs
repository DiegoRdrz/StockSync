using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Domain.Repositories;

public sealed record MovimientoStockDetalle(
    MovimientoStock Movimiento, Guid ProductoId, string ProductoNombre, string ProductoSku, Guid SucursalId);
