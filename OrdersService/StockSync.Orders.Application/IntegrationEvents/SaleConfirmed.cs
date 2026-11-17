using System.Text.Json.Serialization;
using StockSync.Orders.Domain.Entities;

namespace StockSync.Orders.Application.IntegrationEvents;

// A sale was confirmed. Inventory has ALREADY issued the stock of every line,
// so this event is informational (reporting, notifications): consuming it to issue
// stock again would decrement it twice.
public sealed record SaleConfirmed(
    Guid EventId,
    DateTime OccurredAt,
    Guid TenantId,
    Guid OrderId,
    Guid BranchId,
    DateTime ConfirmedAt,
    decimal Total,
    IReadOnlyList<SaleConfirmedLine> Lines) : IIntegrationEvent
{
    public const string Name = "orders.sale-confirmed.v1";

    [JsonIgnore]
    public string EventType => Name;

    public static SaleConfirmed From(Order order, Guid tenantId) => new(
        Guid.NewGuid(),
        DateTime.UtcNow,
        tenantId,
        order.Id,
        order.BranchId,
        order.ConfirmedAt ?? throw new InvalidOperationException("The order has not been confirmed."),
        order.Total,
        order.Lines
            .OrderBy(l => l.LineNumber)
            .Select(l => new SaleConfirmedLine(
                l.LineNumber,
                l.ProductId, l.ProductSku, l.ProductName, l.Quantity, l.UnitPrice, l.LineTotal, l.StockMovementId))
            .ToList());
}

public sealed record SaleConfirmedLine(
    int LineNumber,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    Guid? StockMovementId);
