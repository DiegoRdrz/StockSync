using StockSync.Orders.Application.Common;
using StockSync.Orders.Application.Inventory;

namespace StockSync.Orders.Application.Lines;

// Inventory data needed to add a line: product snapshot, price and the branch stock assignment.
public sealed record LineSource(InventoryProduct Product, Guid StockId, decimal UnitPrice)
{
    // An invalid product in the body is a validation error (400), as in Inventory.
    public static async Task<LineSource> LoadAsync(
        IInventoryClient inventory,
        Guid productId,
        Guid branchId,
        Func<InventoryProduct, decimal> priceSelector,
        string priceName,
        CancellationToken cancellationToken)
    {
        const string field = nameof(LineRequest.ProductId);

        var product = await inventory.GetProductAsync(productId, cancellationToken)
            ?? throw ValidationErrors.Single(field, $"Product '{productId}' does not exist or is inactive.");

        var unitPrice = priceSelector(product);
        if (unitPrice <= 0)
            throw ValidationErrors.Single(field, $"Product '{product.Sku}' has no {priceName} greater than zero in Inventory.");

        var stockId = await inventory.FindStockIdAsync(productId, branchId, cancellationToken)
            ?? throw ValidationErrors.Single(field, $"Product '{product.Sku}' is not assigned to branch '{branchId}' in Inventory.");

        return new LineSource(product, stockId, unitPrice);
    }
}
