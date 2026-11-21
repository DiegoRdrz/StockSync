using System.Text.Json.Serialization;
using StockSync.Reporting.Domain;

namespace StockSync.Reporting.Application;

public sealed record StockMovementRecorded(Guid EventId, DateTime OccurredAt, Guid TenantId, Guid MovementId,
    Guid StockId, Guid ProductId, Guid BranchId, string Type, int Quantity, [property: JsonRequired] int PreviousQuantity,
    [property: JsonRequired] int NewQuantity, DateTime MovementDate, string ProductSku, string ProductName,
    [property: JsonRequired] int MinimumStock, [property: JsonRequired] Guid? CategoryId) : IReportingEvent<StockMovementRecorded>
{
    public static string RoutingKey => "inventory.stock-movement-recorded.v1";
    public static string ConsumerName => "reporting.stock-movements";

    public void Validate()
    {
        if (EventId != MovementId || StockId == Guid.Empty || ProductId == Guid.Empty || BranchId == Guid.Empty ||
            OccurredAt.Kind != DateTimeKind.Utc || MovementDate.Kind != DateTimeKind.Utc || OccurredAt != MovementDate ||
            Type is not ("Receipt" or "Issue" or "Adjustment") || Quantity <= 0 || PreviousQuantity < 0 || NewQuantity < 0 ||
            MinimumStock < 0 || string.IsNullOrWhiteSpace(ProductSku) || string.IsNullOrWhiteSpace(ProductName))
            throw new InvalidDataException("Invalid stock movement fields.");
    }

    public StockMovement ProjectMovement() => new(MovementId, StockId, ProductId, BranchId, Type, Quantity,
        PreviousQuantity, NewQuantity, MovementDate, ProductSku, ProductName, MinimumStock, CategoryId);

    public StockLevel ProjectLevel() => new(StockId, BranchId, ProductId, ProductSku, ProductName,
        NewQuantity, MinimumStock, OccurredAt, MovementId);
}
