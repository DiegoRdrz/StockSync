namespace StockSync.Inventory.Application.Categories;

public sealed class CategoryFilter
{
    public const int MaxPageSize = 100;

    public string? Name { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
