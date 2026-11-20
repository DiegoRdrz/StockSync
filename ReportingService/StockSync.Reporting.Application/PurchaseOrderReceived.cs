using StockSync.Reporting.Domain;

namespace StockSync.Reporting.Application;

public sealed record PurchaseOrderReceived(Guid EventId, DateTime OccurredAt, Guid TenantId, Guid PurchaseOrderId,
    Guid BranchId, string SupplierName, DateTime ReceivedAt, decimal Total, List<OrderLineEvent> Lines) : IReportingEvent<PurchaseOrderReceived>
{
    public static string RoutingKey => "orders.purchase-order-received.v1";
    public static string ConsumerName => "reporting.purchases";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SupplierName) || PurchaseOrderId == Guid.Empty || BranchId == Guid.Empty || OccurredAt.Kind != DateTimeKind.Utc ||
            ReceivedAt.Kind != DateTimeKind.Utc || Total <= 0 || Lines is not { Count: > 0 })
            throw new InvalidDataException("Invalid purchase fields.");
        OrderLineEvent.Validate(Lines);
    }

    public Purchase Project() => new()
    {
        PurchaseOrderId = PurchaseOrderId, BranchId = BranchId, SupplierName = SupplierName, ReceivedAt = ReceivedAt, Total = Total,
        Units = Lines.Sum(l => (long)l.Quantity),
        Lines = Lines.Select(l => new PurchaseLine
        {
            PurchaseOrderId = PurchaseOrderId, LineNumber = l.LineNumber, ProductId = l.ProductId, ProductSku = l.ProductSku,
            ProductName = l.ProductName, Quantity = l.Quantity, UnitPrice = l.UnitPrice,
            LineTotal = l.LineTotal, StockMovementId = l.StockMovementId
        }).ToList()
    };
}
