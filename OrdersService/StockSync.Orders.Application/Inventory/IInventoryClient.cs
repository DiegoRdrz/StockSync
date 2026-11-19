namespace StockSync.Orders.Application.Inventory;

// Port to the Inventory Service. Calls run in the current tenant.
public interface IInventoryClient
{
    // Returns null when the product does not exist or is inactive.
    // Throws InventoryUnavailableException when Inventory cannot answer.
    Task<InventoryProduct?> GetProductAsync(Guid productId, CancellationToken cancellationToken);

    // Returns null when the product is not assigned to the branch.
    // Throws InventoryUnavailableException when Inventory cannot answer.
    Task<Guid?> FindStockIdAsync(Guid productId, Guid branchId, CancellationToken cancellationToken);

    // Movements never throw for communication failures: the result states whether
    // Inventory applied the movement, rejected it, or the outcome is unknown.
    Task<StockMovementResult> IssueAsync(Guid stockId, int quantity, CancellationToken cancellationToken);

    Task<StockMovementResult> ReceiveAsync(Guid stockId, int quantity, CancellationToken cancellationToken);
}
