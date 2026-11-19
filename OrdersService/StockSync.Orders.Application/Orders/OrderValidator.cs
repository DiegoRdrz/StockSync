using StockSync.Orders.Application.Common;
using StockSync.Orders.Domain.Entities;

namespace StockSync.Orders.Application.Orders;

public static class OrderValidator
{
    public static Dictionary<string, string[]> Validate(OrderRequest request)
    {
        var errors = new ValidationErrors();

        if (request.BranchId == Guid.Empty)
            errors.Add(nameof(request.BranchId), "Branch is required.");

        if (request.Notes?.Trim().Length > Order.NotesMaxLength)
            errors.Add(nameof(request.Notes), $"Notes cannot exceed {Order.NotesMaxLength} characters.");

        return errors.ToDictionary();
    }

    public static Dictionary<string, string[]> Validate(OrderFilter filter) =>
        DocumentFilterValidator.Validate(filter);
}
