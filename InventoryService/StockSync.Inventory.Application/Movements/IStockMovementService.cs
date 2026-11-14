using StockSync.Inventory.Application.Common;

namespace StockSync.Inventory.Application.Movements;

public interface IStockMovementService
{
    Task<StockMovementResponse> RecordReceiptAsync(Guid stockId, StockMovementRequest request, CancellationToken cancellationToken);
    Task<StockMovementResponse> RecordIssueAsync(Guid stockId, StockMovementRequest request, CancellationToken cancellationToken);
    Task<StockMovementResponse> GetByIdAsync(Guid stockId, Guid id, CancellationToken cancellationToken);
    Task<PagedResult<StockMovementResponse>> ListAsync(Guid stockId, StockMovementFilter filter, CancellationToken cancellationToken);
    Task<PagedResult<StockMovementListResponse>> ListAllAsync(int page, CancellationToken cancellationToken);
}
