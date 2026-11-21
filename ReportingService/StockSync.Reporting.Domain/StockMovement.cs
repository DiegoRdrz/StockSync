namespace StockSync.Reporting.Domain;

public sealed record StockMovement(Guid MovementId, Guid StockId, Guid ProductId, Guid BranchId, string Type,
    int Quantity, int PreviousQuantity, int NewQuantity, DateTime MovementDate, string ProductSku, string ProductName,
    int MinimumStock, Guid? CategoryId);

public sealed record StockLevel(Guid StockId, Guid BranchId, Guid ProductId, string ProductSku, string ProductName,
    int Quantity, int MinimumStock, DateTime LastOccurredAt, Guid LastMovementId);
