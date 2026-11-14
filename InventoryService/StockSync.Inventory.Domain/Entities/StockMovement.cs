using StockSync.Inventory.Domain.Common;

namespace StockSync.Inventory.Domain.Entities;

// Created only when a valid movement is applied to stock.
public class StockMovement
{
    public Guid Id { get; private set; }
    public Guid StockId { get; private set; }
    public StockMovementType Type { get; private set; }
    public int Quantity { get; private set; }
    public int PreviousQuantity { get; private set; }
    public int NewQuantity { get; private set; }
    public DateTime Date { get; private set; }

    private StockMovement()
    {
    }

    internal StockMovement(Stock stock, StockMovementType type, int quantity, int previousQuantity)
    {
        Id = Guid.NewGuid();
        StockId = stock.Id;
        Type = type;
        Quantity = quantity;
        PreviousQuantity = previousQuantity;
        NewQuantity = stock.Quantity;
        Date = UtcClock.Now();
    }
}
