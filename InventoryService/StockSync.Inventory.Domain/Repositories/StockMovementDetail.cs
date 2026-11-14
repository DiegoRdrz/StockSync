using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Domain.Repositories;

public sealed record StockMovementDetail(
    StockMovement Movement, Guid ProductId, string ProductName, string ProductSku, Guid BranchId);
