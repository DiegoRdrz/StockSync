using StockSync.Orders.Application.Common;
using StockSync.Orders.Domain.Entities;

namespace StockSync.Orders.Application.Orders;

public sealed class OrderFilter : DocumentFilter
{
    public OrderStatus? Status { get; init; }
}
