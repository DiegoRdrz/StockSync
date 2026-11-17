using StockSync.Orders.Application.Common;
using StockSync.Orders.Domain.Entities;

namespace StockSync.Orders.Application.PurchaseOrders;

public sealed class PurchaseOrderFilter : DocumentFilter
{
    public PurchaseOrderStatus? Status { get; init; }
}
