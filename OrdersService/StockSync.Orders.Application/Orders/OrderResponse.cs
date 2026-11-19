using StockSync.Orders.Application.Lines;
using StockSync.Orders.Domain.Entities;

namespace StockSync.Orders.Application.Orders;

public sealed record OrderResponse(
    Guid Id,
    Guid BranchId,
    string Status,
    string? Notes,
    decimal Total,
    string? ReviewReason,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? ConfirmedAt,
    DateTime? CancelledAt,
    IReadOnlyList<LineResponse> Lines)
{
    public static OrderResponse From(Order order) => new(
        order.Id,
        order.BranchId,
        order.Status.ToString(),
        order.Notes,
        order.Total,
        order.ReviewReason,
        order.CreatedAt,
        order.UpdatedAt,
        order.ConfirmedAt,
        order.CancelledAt,
        LineResponse.FromLines(order.Lines));
}
