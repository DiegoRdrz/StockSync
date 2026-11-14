namespace StockSync.Inventory.Application.Stocks;

public static class StockValidator
{
    public static Dictionary<string, string[]> Validate(StockRequest request)
    {
        var errors = new Dictionary<string, List<string>>();

        if (request.ProductId == Guid.Empty)
            Add(errors, nameof(request.ProductId), "Product is required.");

        if (request.BranchId == Guid.Empty)
            Add(errors, nameof(request.BranchId), "Branch is required.");

        if (request.Quantity < 0)
            Add(errors, nameof(request.Quantity), "Quantity cannot be negative.");

        return Convert(errors);
    }

    public static Dictionary<string, string[]> Validate(StockQuantityRequest request)
    {
        var errors = new Dictionary<string, List<string>>();

        if (request.NewQuantity < 0)
            Add(errors, nameof(request.NewQuantity), "Quantity cannot be negative.");

        return Convert(errors);
    }

    public static Dictionary<string, string[]> Validate(StockFilter filter)
    {
        var errors = new Dictionary<string, List<string>>();

        if (filter.Page < 1)
            Add(errors, nameof(filter.Page), "Page must be at least 1.");

        if (filter.PageSize < 1 || filter.PageSize > StockFilter.MaxPageSize)
            Add(errors, nameof(filter.PageSize), $"Page size must be between 1 and {StockFilter.MaxPageSize}.");

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
