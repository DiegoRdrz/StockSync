namespace StockSync.Orders.Domain.Entities;

public class PurchaseOrderLine : StockLine
{
    public Guid PurchaseOrderId { get; private set; }

    private PurchaseOrderLine()
    {
    }

    internal PurchaseOrderLine(
        Guid purchaseOrderId,
        int lineNumber,
        Guid productId,
        Guid stockId,
        string productName,
        string productSku,
        int quantity,
        decimal unitPrice)
        : base(lineNumber, productId, stockId, productName, productSku, quantity, unitPrice)
    {
        PurchaseOrderId = purchaseOrderId;
    }
}
