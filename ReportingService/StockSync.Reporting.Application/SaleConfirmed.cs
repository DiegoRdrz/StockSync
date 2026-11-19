using StockSync.Reporting.Domain;

namespace StockSync.Reporting.Application;

public sealed record SaleConfirmed(Guid EventId, DateTime OccurredAt, Guid TenantId, Guid OrderId,
    Guid BranchId, DateTime ConfirmedAt, decimal Total, List<OrderLineEvent> Lines) : IReportingEvent<SaleConfirmed>
{
    public static string RoutingKey => "orders.sale-confirmed.v1";
    public static string ConsumerName => "reporting.sales";

    public void Validate()
    {
        if (OrderId == Guid.Empty || BranchId == Guid.Empty || OccurredAt.Kind != DateTimeKind.Utc ||
            ConfirmedAt.Kind != DateTimeKind.Utc || Total <= 0 || Lines is not { Count: > 0 })
            throw new InvalidDataException("Invalid sale fields.");
        OrderLineEvent.Validate(Lines);
    }

    public Sale Project() => new()
    {
        OrderId = OrderId, BranchId = BranchId, ConfirmedAt = ConfirmedAt, Total = Total,
        Units = Lines.Sum(l => (long)l.Quantity),
        Lines = Lines.Select(l => new SaleLine
        {
            OrderId = OrderId, LineNumber = l.LineNumber, ProductId = l.ProductId, ProductSku = l.ProductSku,
            ProductName = l.ProductName, Quantity = l.Quantity, UnitPrice = l.UnitPrice,
            LineTotal = l.LineTotal, StockMovementId = l.StockMovementId
        }).ToList()
    };
}
