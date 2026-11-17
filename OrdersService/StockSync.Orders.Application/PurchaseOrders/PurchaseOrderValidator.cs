using StockSync.Orders.Application.Common;
using StockSync.Orders.Domain.Entities;

namespace StockSync.Orders.Application.PurchaseOrders;

public static class PurchaseOrderValidator
{
    public static Dictionary<string, string[]> Validate(PurchaseOrderRequest request)
    {
        var errors = new ValidationErrors();

        if (request.BranchId == Guid.Empty)
            errors.Add(nameof(request.BranchId), "Branch is required.");

        if (string.IsNullOrWhiteSpace(request.SupplierName))
            errors.Add(nameof(request.SupplierName), "Supplier name is required.");
        else if (request.SupplierName.Trim().Length > PurchaseOrder.SupplierNameMaxLength)
            errors.Add(nameof(request.SupplierName),
                $"Supplier name cannot exceed {PurchaseOrder.SupplierNameMaxLength} characters.");

        if (request.Notes?.Trim().Length > PurchaseOrder.NotesMaxLength)
            errors.Add(nameof(request.Notes), $"Notes cannot exceed {PurchaseOrder.NotesMaxLength} characters.");

        return errors.ToDictionary();
    }

    public static Dictionary<string, string[]> Validate(PurchaseOrderFilter filter) =>
        DocumentFilterValidator.Validate(filter);
}
