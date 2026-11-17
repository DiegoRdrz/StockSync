namespace StockSync.Orders.Application.Inventory;

// Product data Orders copies into its lines. Inventory owns prices:
// sales use SalePrice and purchase orders use PurchasePrice.
public sealed record InventoryProduct(
    Guid Id,
    string Name,
    string Sku,
    decimal PurchasePrice,
    decimal SalePrice);
