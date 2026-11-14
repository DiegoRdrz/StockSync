namespace StockSync.Inventory.Application.Products;

public sealed record ProductRequest(
    string Name,
    string Sku,
    string? Description,
    decimal PurchasePrice,
    decimal SalePrice,
    int MinimumStock,
    Guid? CategoryId);
