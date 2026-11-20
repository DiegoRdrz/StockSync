using System.Text.Json.Serialization;
using StockSync.Orders.Domain.Entities;

namespace StockSync.Orders.Application.IntegrationEvents;

// Inventory has already recorded the receipts, so consumers can report on the purchase without applying stock changes.
public sealed record PurchaseOrderReceived(
    Guid EventId,
    DateTime OccurredAt,
    Guid TenantId,
    Guid PurchaseOrderId,
    Guid BranchId,
    string SupplierName,
    DateTime ReceivedAt,
    decimal Total,
    IReadOnlyList<PurchaseOrderReceivedLine> Lines) : IIntegrationEvent
{
    public const string Name = "orders.purchase-order-received.v1";

    [JsonIgnore]
    public string EventType => Name;

    public static PurchaseOrderReceived From(PurchaseOrder purchaseOrder, Guid tenantId) => new(
        Guid.NewGuid(),
        DateTime.UtcNow,
        tenantId,
        purchaseOrder.Id,
        purchaseOrder.BranchId,
        purchaseOrder.SupplierName,
        purchaseOrder.ReceivedAt ?? throw new InvalidOperationException("The purchase order has not been received."),
        purchaseOrder.Total,
        purchaseOrder.Lines
            .OrderBy(l => l.LineNumber)
            .Select(l => new PurchaseOrderReceivedLine(
                l.LineNumber,
                l.ProductId, l.ProductSku, l.ProductName, l.Quantity, l.UnitPrice, l.LineTotal, l.StockMovementId))
            .ToList());
}

public sealed record PurchaseOrderReceivedLine(
    int LineNumber,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    Guid? StockMovementId);
