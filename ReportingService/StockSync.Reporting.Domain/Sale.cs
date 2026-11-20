namespace StockSync.Reporting.Domain;

public sealed class Sale
{
    public Guid OrderId { get; init; }
    public Guid BranchId { get; init; }
    public DateTime ConfirmedAt { get; init; }
    public decimal Total { get; init; }
    public long Units { get; init; }
    public List<SaleLine> Lines { get; init; } = [];
}

public sealed class SaleLine
{
    public Guid OrderId { get; init; }
    public int LineNumber { get; init; }
    public Guid ProductId { get; init; }
    public required string ProductSku { get; init; }
    public required string ProductName { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal LineTotal { get; init; }
    public Guid? StockMovementId { get; init; }
}
