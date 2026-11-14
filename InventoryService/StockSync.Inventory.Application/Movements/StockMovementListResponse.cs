using StockSync.Inventory.Domain.Repositories;

namespace StockSync.Inventory.Application.Movements;

public sealed record StockMovementListResponse(
    Guid Id, Guid StockId, Guid ProductId, string ProductName, string ProductSku,
    Guid BranchId, string Type, int Quantity, int PreviousQuantity, int NewQuantity, DateTime Date)
{
    public static StockMovementListResponse From(StockMovementDetail detail) => new(
        detail.Movement.Id, detail.Movement.StockId, detail.ProductId,
        detail.ProductName, detail.ProductSku, detail.BranchId,
        detail.Movement.Type.ToString(), detail.Movement.Quantity,
        detail.Movement.PreviousQuantity, detail.Movement.NewQuantity, detail.Movement.Date);
}
