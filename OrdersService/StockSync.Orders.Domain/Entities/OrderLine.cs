namespace StockSync.Orders.Domain.Entities;

public class OrderLine : StockLine
{
    public Guid OrderId { get; private set; }

    private OrderLine()
    {
    }

    internal OrderLine(
        Guid orderId,
        int lineNumber,
        Guid productId,
        Guid stockId,
        string productName,
        string productSku,
        int quantity,
        decimal unitPrice)
        : base(lineNumber, productId, stockId, productName, productSku, quantity, unitPrice)
    {
        OrderId = orderId;
    }
}
