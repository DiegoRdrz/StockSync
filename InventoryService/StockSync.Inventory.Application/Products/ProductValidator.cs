using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Application.Products;

public static class ProductValidator
{
    public static Dictionary<string, string[]> Validate(ProductRequest request)
    {
        var errors = new Dictionary<string, List<string>>();

        if (string.IsNullOrWhiteSpace(request.Name))
            Add(errors, nameof(request.Name), "Name is required.");
        else if (request.Name.Trim().Length > Product.NameMaxLength)
            Add(errors, nameof(request.Name), $"Name cannot exceed {Product.NameMaxLength} characters.");

        if (string.IsNullOrWhiteSpace(request.Sku))
            Add(errors, nameof(request.Sku), "SKU is required.");
        else if (request.Sku.Trim().Length > Product.SkuMaxLength)
            Add(errors, nameof(request.Sku), $"SKU cannot exceed {Product.SkuMaxLength} characters.");

        if (request.Description?.Trim().Length > Product.DescriptionMaxLength)
            Add(errors, nameof(request.Description), $"Description cannot exceed {Product.DescriptionMaxLength} characters.");

        if (request.PurchasePrice < 0)
            Add(errors, nameof(request.PurchasePrice), "Purchase price cannot be negative.");
        else if (!Product.IsRepresentablePrice(request.PurchasePrice))
            Add(errors, nameof(request.PurchasePrice), $"Purchase price allows at most {Product.PriceDecimalPlaces} decimal places and cannot exceed {Product.MaxPrice}.");

        if (request.SalePrice < 0)
            Add(errors, nameof(request.SalePrice), "Sale price cannot be negative.");
        else if (!Product.IsRepresentablePrice(request.SalePrice))
            Add(errors, nameof(request.SalePrice), $"Sale price allows at most {Product.PriceDecimalPlaces} decimal places and cannot exceed {Product.MaxPrice}.");

        if (request.MinimumStock < 0)
            Add(errors, nameof(request.MinimumStock), "Minimum stock cannot be negative.");

        if (request.CategoryId == Guid.Empty)
            Add(errors, nameof(request.CategoryId), "The specified category is invalid.");

        return Convert(errors);
    }

    public static Dictionary<string, string[]> Validate(ProductFilter filter)
    {
        var errors = new Dictionary<string, List<string>>();

        if (filter.Page < 1)
            Add(errors, nameof(filter.Page), "Page must be at least 1.");

        if (filter.PageSize < 1 || filter.PageSize > ProductFilter.MaxPageSize)
            Add(errors, nameof(filter.PageSize), $"Page size must be between 1 and {ProductFilter.MaxPageSize}.");

        // The offset (Page - 1) * PageSize is an int and must not overflow.
        if (filter.Page > 0 && filter.PageSize > 0 &&
            (long)(filter.Page - 1) * filter.PageSize > int.MaxValue)
            Add(errors, nameof(filter.Page), "The requested page exceeds the allowed limit.");

        return Convert(errors);
    }

    private static void Add(Dictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var messages))
            errors[field] = messages = [];
        messages.Add(message);
    }

    private static Dictionary<string, string[]> Convert(Dictionary<string, List<string>> errors) =>
        errors.ToDictionary(e => e.Key, e => e.Value.ToArray());
}
