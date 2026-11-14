using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Application.Categories;

public sealed record CategoryResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public static CategoryResponse From(Category category) => new(
        category.Id,
        category.Name,
        category.Description,
        category.IsActive,
        category.CreatedAt,
        category.UpdatedAt);
}
