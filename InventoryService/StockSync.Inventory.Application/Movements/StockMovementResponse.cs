using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Application.Movements;

public sealed record StockMovementResponse(
    Guid Id, Guid StockId, string Type, int Quantity,
    int PreviousQuantity, int NewQuantity, DateTime Date)
{
    public static StockMovementResponse From(StockMovement movement) => new(
        movement.Id, movement.StockId, movement.Type.ToString(), movement.Quantity,
        movement.PreviousQuantity, movement.NewQuantity, movement.Date);
}
