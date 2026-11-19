namespace StockSync.Orders.Domain.Entities;

// Progress of the stock movement that a line requests from Inventory.
public enum StockLineStatus
{
    // No movement has been recorded in Inventory for this line.
    Pending = 1,
    // Inventory recorded the movement (issue for sales, receipt for purchases).
    Applied = 2,
    // The applied movement was reversed with the opposite movement.
    Compensated = 3
}
