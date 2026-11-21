using System.Text.Json.Serialization;

namespace StockSync.Notification.Application.IntegrationEvents;

public sealed record OrderCreatedEvent(
    [property: JsonRequired] Guid EventId,
    [property: JsonRequired] DateTime OccurredAt,
    [property: JsonRequired] Guid TenantId,
    [property: JsonRequired] Guid OrderId,
    [property: JsonRequired] Guid BranchId,
    [property: JsonRequired] DateTime CreatedAt) : INotificationEvent<OrderCreatedEvent>
{
    public static string RoutingKey => "orders.order-created.v1";
    public static string ConsumerName => "notification.orders";

    public void Validate()
    {
        if (OrderId == Guid.Empty || BranchId == Guid.Empty ||
            OccurredAt.Kind != DateTimeKind.Utc || CreatedAt.Kind != DateTimeKind.Utc)
            throw new InvalidDataException("Invalid order created event fields.");
    }
}
