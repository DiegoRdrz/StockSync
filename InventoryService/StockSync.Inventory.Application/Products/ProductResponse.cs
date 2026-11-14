using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Application.Products;

public sealed record ProductResponse(
    Guid Id,
    string Name,
    string Sku,
    string? Description,
    decimal PurchasePrice,
    decimal SalePrice,
    int MinimumStock,
    Guid? CategoryId,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public static ProductResponse From(Product product) => new(
        product.Id,
        product.Name,
        product.Sku,
        product.Description,
        product.PurchasePrice,
        product.SalePrice,
        product.MinimumStock,
        product.CategoryId,
        product.IsActive,
        product.CreatedAt,
        product.UpdatedAt);
}
