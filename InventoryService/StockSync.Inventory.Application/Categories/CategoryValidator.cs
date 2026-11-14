using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Application.Categories;

public static class CategoryValidator
{
    public static Dictionary<string, string[]> Validate(CategoryRequest request)
    {
        var errors = new Dictionary<string, List<string>>();

        if (string.IsNullOrWhiteSpace(request.Name))
            Add(errors, nameof(request.Name), "Name is required.");
        else if (request.Name.Trim().Length > Category.NameMaxLength)
            Add(errors, nameof(request.Name), $"Name cannot exceed {Category.NameMaxLength} characters.");

        if (request.Description?.Trim().Length > Category.DescriptionMaxLength)
            Add(errors, nameof(request.Description), $"Description cannot exceed {Category.DescriptionMaxLength} characters.");

        return Convert(errors);
    }

    public static Dictionary<string, string[]> Validate(CategoryFilter filter)
    {
        var errors = new Dictionary<string, List<string>>();

        if (filter.Page < 1)
            Add(errors, nameof(filter.Page), "Page must be at least 1.");

        if (filter.PageSize < 1 || filter.PageSize > CategoryFilter.MaxPageSize)
            Add(errors, nameof(filter.PageSize), $"Page size must be between 1 and {CategoryFilter.MaxPageSize}.");

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
