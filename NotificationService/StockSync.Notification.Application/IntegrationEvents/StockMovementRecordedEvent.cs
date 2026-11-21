using System.Text.Json.Serialization;

namespace StockSync.Notification.Application.IntegrationEvents;

public sealed record StockMovementRecordedEvent(
    [property: JsonRequired] Guid EventId,
    [property: JsonRequired] DateTime OccurredAt,
    [property: JsonRequired] Guid TenantId,
    [property: JsonRequired] Guid MovementId,
    [property: JsonRequired] Guid StockId,
    [property: JsonRequired] Guid ProductId,
    [property: JsonRequired] Guid BranchId,
    [property: JsonRequired] string Type,
    [property: JsonRequired] int Quantity,
    [property: JsonRequired] int PreviousQuantity,
    [property: JsonRequired] int NewQuantity,
    [property: JsonRequired] DateTime MovementDate,
    [property: JsonRequired] string ProductSku,
    [property: JsonRequired] string ProductName,
    [property: JsonRequired] int MinimumStock,
    Guid? CategoryId) : INotificationEvent<StockMovementRecordedEvent>
{
    public static string RoutingKey => "inventory.stock-movement-recorded.v1";
    public static string ConsumerName => "notification.stock-movements";

    public int Shortage => Math.Max(0, MinimumStock - NewQuantity);

    // Low-stock alerts fire when an active product has a positive minimum and the movement
    // brings or drops the balance below that minimum (including initial balance adjustments).
    public bool TriggersLowStockAlert =>
        MinimumStock > 0
        && NewQuantity < MinimumStock
        && (PreviousQuantity >= MinimumStock
            || NewQuantity < PreviousQuantity
            || (Type == "Adjustment" && PreviousQuantity == 0));

    public void Validate()
    {
        if (EventId != MovementId || StockId == Guid.Empty || ProductId == Guid.Empty || BranchId == Guid.Empty ||
            OccurredAt.Kind != DateTimeKind.Utc || MovementDate.Kind != DateTimeKind.Utc || OccurredAt != MovementDate ||
            Type is not ("Receipt" or "Issue" or "Adjustment") || Quantity <= 0 || PreviousQuantity < 0 || NewQuantity < 0 ||
            MinimumStock < 0 || string.IsNullOrWhiteSpace(ProductSku) || string.IsNullOrWhiteSpace(ProductName))
            throw new InvalidDataException("Invalid stock movement event fields.");
    }
}
