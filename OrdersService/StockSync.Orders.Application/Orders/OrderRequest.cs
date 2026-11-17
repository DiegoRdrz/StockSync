namespace StockSync.Orders.Application.Orders;

public sealed record OrderRequest(Guid BranchId, string? Notes);
