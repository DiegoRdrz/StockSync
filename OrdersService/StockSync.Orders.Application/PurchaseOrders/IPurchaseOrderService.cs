using StockSync.Orders.Application.Common;
using StockSync.Orders.Application.Lines;

namespace StockSync.Orders.Application.PurchaseOrders;

public interface IPurchaseOrderService
{
    Task<PurchaseOrderResponse> CreateAsync(PurchaseOrderRequest request, CancellationToken cancellationToken);

    Task<PurchaseOrderResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<PurchaseOrderResponse>> ListAsync(PurchaseOrderFilter filter, CancellationToken cancellationToken);

    Task<PurchaseOrderResponse> AddLineAsync(Guid id, LineRequest request, CancellationToken cancellationToken);

    Task<PurchaseOrderResponse> UpdateLineAsync(Guid id, Guid lineId, LineQuantityRequest request, CancellationToken cancellationToken);

    Task<PurchaseOrderResponse> RemoveLineAsync(Guid id, Guid lineId, CancellationToken cancellationToken);

    Task<PurchaseOrderResponse> PlaceAsync(Guid id, CancellationToken cancellationToken);

    Task<PurchaseOrderResponse> ReceiveAsync(Guid id, CancellationToken cancellationToken);

    Task<PurchaseOrderResponse> CancelAsync(Guid id, CancellationToken cancellationToken);
}
