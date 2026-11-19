namespace StockSync.Orders.Domain.Entities;

// A document whose lines move stock in Inventory: sales issue stock, purchases receive it.
// The stock saga uses this contract to record each movement on the document.
public interface IStockDocument
{
    Guid Id { get; }

    IReadOnlyCollection<StockLine> StockLines { get; }

    void MarkLineApplied(Guid lineId, Guid movementId);

    void MarkLineCompensated(Guid lineId, Guid movementId);
}
