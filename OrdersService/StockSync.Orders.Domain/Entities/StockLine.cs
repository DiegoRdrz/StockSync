using StockSync.Orders.Domain.Common;
using StockSync.Orders.Domain.Exceptions;

namespace StockSync.Orders.Domain.Entities;

// Common data of sales and purchase lines. Product data is a snapshot taken from Inventory
// when the line is added, so later catalog changes do not alter the document.
// The base class is not mapped by itself; each line type has its own table.
public abstract class StockLine
{
    public const int ProductNameMaxLength = 150;
    public const int ProductSkuMaxLength = 50;

    public Guid Id { get; private set; }
    // Position in the document (1, 2, 3...): display order and the order in which stock moves.
    public int LineNumber { get; private set; }
    public Guid ProductId { get; private set; }
    // Inventory stock assignment (product + branch) that receives the movement.
    public Guid StockId { get; private set; }
    public string ProductName { get; private set; } = null!;
    public string ProductSku { get; private set; } = null!;
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal LineTotal { get; private set; }
    public StockLineStatus StockStatus { get; private set; }
    // Inventory movement IDs let a reviewer match the line with Inventory history.
    public Guid? StockMovementId { get; private set; }
    public Guid? CompensationMovementId { get; private set; }

    protected StockLine()
    {
    }

    protected StockLine(
        int lineNumber,
        Guid productId,
        Guid stockId,
        string productName,
        string productSku,
        int quantity,
        decimal unitPrice)
    {
        if (lineNumber <= 0)
            throw new DomainException("The line number must be greater than zero.");
        if (productId == Guid.Empty)
            throw new DomainException("The specified product is invalid.");
        if (stockId == Guid.Empty)
            throw new DomainException("The specified stock is invalid.");
        if (string.IsNullOrWhiteSpace(productName))
            throw new DomainException("Product name is required.");
        if (string.IsNullOrWhiteSpace(productSku))
            throw new DomainException("Product SKU is required.");
        if (unitPrice <= 0)
            throw new DomainException("Unit price must be greater than zero.");
        if (!Amount.IsRepresentable(unitPrice))
            throw new DomainException($"Unit price allows at most {Amount.DecimalPlaces} decimal places and cannot exceed {Amount.MaxValue}.");

        Id = Guid.NewGuid();
        LineNumber = lineNumber;
        ProductId = productId;
        StockId = stockId;
        ProductName = Truncate(productName.Trim(), ProductNameMaxLength);
        ProductSku = Truncate(productSku.Trim(), ProductSkuMaxLength);
        UnitPrice = unitPrice;
        StockStatus = StockLineStatus.Pending;
        SetQuantity(quantity);
    }

    internal void SetQuantity(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Quantity must be greater than zero.");

        var lineTotal = quantity * UnitPrice;
        if (lineTotal > Amount.MaxValue)
            throw new DomainException($"The line total cannot exceed {Amount.MaxValue}.");

        Quantity = quantity;
        LineTotal = lineTotal;
    }

    internal void MarkApplied(Guid movementId)
    {
        if (StockStatus != StockLineStatus.Pending)
            throw new DomainConflictException($"Line '{Id}' already has a stock movement.");
        if (movementId == Guid.Empty)
            throw new DomainException("The stock movement is invalid.");

        StockStatus = StockLineStatus.Applied;
        StockMovementId = movementId;
    }

    internal void MarkCompensated(Guid movementId)
    {
        if (StockStatus != StockLineStatus.Applied)
            throw new DomainConflictException($"Line '{Id}' has no stock movement to compensate.");
        if (movementId == Guid.Empty)
            throw new DomainException("The compensation movement is invalid.");

        StockStatus = StockLineStatus.Compensated;
        CompensationMovementId = movementId;
    }

    // Used when every applied movement was compensated and the document can be edited again.
    internal void ResetStockStatus()
    {
        StockStatus = StockLineStatus.Pending;
        StockMovementId = null;
        CompensationMovementId = null;
    }

    // Inventory already limits these lengths; truncation only protects the snapshot columns.
    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
