namespace StockSync.Orders.Application.Inventory;

public enum StockMovementOutcome
{
    // Inventory recorded the movement.
    Applied = 1,
    // Inventory answered and refused the movement (insufficient stock, inactive product...).
    Rejected = 2,
    // The request never reached Inventory (connection refused), so nothing changed.
    NotApplied = 3,
    // The request may or may not have been applied (timeout, server error, unreadable answer).
    Unknown = 4
}

public sealed record StockMovementResult(StockMovementOutcome Outcome, Guid? MovementId, string? Error)
{
    public static StockMovementResult Applied(Guid movementId) => new(StockMovementOutcome.Applied, movementId, null);

    public static StockMovementResult Rejected(string error) => new(StockMovementOutcome.Rejected, null, error);

    public static StockMovementResult NotApplied(string error) => new(StockMovementOutcome.NotApplied, null, error);

    public static StockMovementResult Unknown(string error) => new(StockMovementOutcome.Unknown, null, error);
}
