using StockSync.Orders.Application.Common;
using StockSync.Orders.Application.Lines;

namespace StockSync.Orders.Application.Orders;

public interface IOrderService
{
    Task<OrderResponse> CreateAsync(OrderRequest request, CancellationToken cancellationToken);

    Task<OrderResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<OrderResponse>> ListAsync(OrderFilter filter, CancellationToken cancellationToken);

    Task<OrderResponse> AddLineAsync(Guid id, LineRequest request, CancellationToken cancellationToken);

    Task<OrderResponse> UpdateLineAsync(Guid id, Guid lineId, LineQuantityRequest request, CancellationToken cancellationToken);

    Task<OrderResponse> RemoveLineAsync(Guid id, Guid lineId, CancellationToken cancellationToken);

    Task<OrderResponse> ConfirmAsync(Guid id, CancellationToken cancellationToken);

    Task<OrderResponse> CancelAsync(Guid id, CancellationToken cancellationToken);
}
