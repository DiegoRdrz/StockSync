using System.Text.Json.Serialization;
using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Application.IntegrationEvents;

public sealed record StockMovementRecorded(
    Guid EventId,
    DateTime OccurredAt,
    Guid TenantId,
    Guid MovementId,
    Guid StockId,
    Guid ProductId,
    Guid BranchId,
    string Type,
    int Quantity,
    int PreviousQuantity,
    int NewQuantity,
    DateTime MovementDate,
    string ProductSku,
    string ProductName,
    int MinimumStock,
    Guid? CategoryId) : IIntegrationEvent
{
    public const string Name = "inventory.stock-movement-recorded.v1";

    [JsonIgnore]
    public string EventType => Name;

    public static StockMovementRecorded From(StockMovement movement, Stock stock, Product product, Guid tenantId) =>
        new(movement.Id, movement.Date, tenantId, movement.Id, movement.StockId, product.Id, stock.BranchId,
            movement.Type.ToString(), movement.Quantity, movement.PreviousQuantity, movement.NewQuantity,
            movement.Date, product.Sku, product.Name, product.MinimumStock, product.CategoryId);
}
