using StockSync.Orders.Application.Lines;
using StockSync.Orders.Domain.Entities;

namespace StockSync.Orders.Application.PurchaseOrders;

public sealed record PurchaseOrderResponse(
    Guid Id,
    Guid BranchId,
    string SupplierName,
    string Status,
    string? Notes,
    decimal Total,
    string? ReviewReason,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? OrderedAt,
    DateTime? ReceivedAt,
    DateTime? CancelledAt,
    IReadOnlyList<LineResponse> Lines)
{
    public static PurchaseOrderResponse From(PurchaseOrder purchaseOrder) => new(
        purchaseOrder.Id,
        purchaseOrder.BranchId,
        purchaseOrder.SupplierName,
        purchaseOrder.Status.ToString(),
        purchaseOrder.Notes,
        purchaseOrder.Total,
        purchaseOrder.ReviewReason,
        purchaseOrder.CreatedAt,
        purchaseOrder.UpdatedAt,
        purchaseOrder.OrderedAt,
        purchaseOrder.ReceivedAt,
        purchaseOrder.CancelledAt,
        LineResponse.FromLines(purchaseOrder.Lines));
}
