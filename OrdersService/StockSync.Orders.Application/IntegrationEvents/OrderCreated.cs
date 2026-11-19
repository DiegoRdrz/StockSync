using System.Text.Json.Serialization;
using StockSync.Orders.Domain.Entities;

namespace StockSync.Orders.Application.IntegrationEvents;

// A sales order was created in draft. No stock has changed yet.
public sealed record OrderCreated(
    Guid EventId,
    DateTime OccurredAt,
    Guid TenantId,
    Guid OrderId,
    Guid BranchId,
    DateTime CreatedAt) : IIntegrationEvent
{
    public const string Name = "orders.order-created.v1";

    [JsonIgnore]
    public string EventType => Name;

    public static OrderCreated From(Order order, Guid tenantId) => new(
        Guid.NewGuid(),
        DateTime.UtcNow,
        tenantId,
        order.Id,
        order.BranchId,
        order.CreatedAt);
}
