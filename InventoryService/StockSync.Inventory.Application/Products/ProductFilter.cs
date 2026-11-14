namespace StockSync.Inventory.Application.Products;

public sealed class ProductFilter
{
    public const int MaxPageSize = 100;

    public string? Name { get; init; }
    public string? Sku { get; init; }
    public Guid? CategoryId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
