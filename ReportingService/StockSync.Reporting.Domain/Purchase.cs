namespace StockSync.Reporting.Domain;

public sealed class Purchase
{
    public Guid PurchaseOrderId { get; init; }
    public Guid BranchId { get; init; }
    public required string SupplierName { get; init; }
    public DateTime ReceivedAt { get; init; }
    public decimal Total { get; init; }
    public long Units { get; init; }
    public List<PurchaseLine> Lines { get; init; } = [];
}

public sealed class PurchaseLine
{
    public Guid PurchaseOrderId { get; init; }
    public int LineNumber { get; init; }
    public Guid ProductId { get; init; }
    public required string ProductSku { get; init; }
    public required string ProductName { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal LineTotal { get; init; }
    public Guid? StockMovementId { get; init; }
}
