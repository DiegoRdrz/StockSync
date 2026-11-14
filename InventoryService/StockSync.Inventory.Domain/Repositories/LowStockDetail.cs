using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Domain.Repositories;

public sealed record LowStockDetail(Stock Stock, string ProductName, string ProductSku, int MinimumStock);
