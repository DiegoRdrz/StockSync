using StockSync.Orders.Application.Common;

namespace StockSync.Orders.Application.Lines;

public static class LineValidator
{
    public static Dictionary<string, string[]> Validate(LineRequest request)
    {
        var errors = new ValidationErrors();

        if (request.ProductId == Guid.Empty)
            errors.Add(nameof(request.ProductId), "Product is required.");

        if (request.Quantity <= 0)
            errors.Add(nameof(request.Quantity), "Quantity must be greater than zero.");

        return errors.ToDictionary();
    }

    public static Dictionary<string, string[]> Validate(LineQuantityRequest request)
    {
        var errors = new ValidationErrors();

        if (request.Quantity <= 0)
            errors.Add(nameof(request.Quantity), "Quantity must be greater than zero.");

        return errors.ToDictionary();
    }
}
