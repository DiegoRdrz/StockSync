namespace StockSync.Orders.Application.PurchaseOrders;

public sealed record PurchaseOrderRequest(Guid BranchId, string SupplierName, string? Notes);
