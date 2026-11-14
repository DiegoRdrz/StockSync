using StockSync.Inventory.Application.Common;

namespace StockSync.Inventory.Application.Stocks;

public interface IStockService
{
    Task<StockResponse> CreateAsync(StockRequest request, CancellationToken cancellationToken);

    Task<StockResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<StockResponse>> ListByBranchAsync(
        Guid branchId, StockFilter filter, CancellationToken cancellationToken);

    Task<PagedResult<StockResponse>> ListByProductAsync(
        Guid productId, StockFilter filter, CancellationToken cancellationToken);

    Task<PagedResult<LowStockResponse>> ListLowStockAsync(
        LowStockFilter filter, CancellationToken cancellationToken);

    Task<StockResponse> UpdateQuantityAsync(Guid id, StockQuantityRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
