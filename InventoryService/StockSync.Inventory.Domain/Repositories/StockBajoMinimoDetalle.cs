using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Domain.Repositories;

public sealed record StockBajoMinimoDetalle(Stock Stock, string ProductoNombre, string ProductoSku, int StockMinimo);
