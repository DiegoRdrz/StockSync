namespace StockSync.Inventory.Application.Movements;

public static class StockMovementValidator
{
    public static Dictionary<string, string[]> Validate(StockMovementRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Quantity <= 0)
            errors[nameof(request.Quantity)] = ["Quantity must be greater than zero."];
        return errors;
    }

    public static Dictionary<string, string[]> Validate(StockMovementFilter filter)
    {
        var errors = new Dictionary<string, string[]>();
        if (filter.Page < 1)
            errors[nameof(filter.Page)] = ["Page must be greater than zero."];
        if (filter.PageSize is < 1 or > 100)
            errors[nameof(filter.PageSize)] = ["Page size must be between 1 and 100."];
        if (filter.Page > 0 && filter.PageSize > 0 &&
            (long)(filter.Page - 1) * filter.PageSize > int.MaxValue)
            errors[nameof(filter.Page)] = ["The requested page exceeds the allowed limit."];
        return errors;
    }
}
