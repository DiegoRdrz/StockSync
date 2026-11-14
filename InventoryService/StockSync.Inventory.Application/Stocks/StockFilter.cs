namespace StockSync.Inventory.Application.Stocks;

public class StockFilter
{
    public const int MaxPageSize = 100;

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed class LowStockFilter : StockFilter
{
    public Guid? BranchId { get; init; }
}
