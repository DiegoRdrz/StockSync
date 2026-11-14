using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Domain.Repositories;

public interface IStockRepository
{
    Task<Stock?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    // Lock the row until the current transaction ends (see IUnitOfWork).
    // Concurrent operations on the same stock wait and read the committed balance.
    Task<Stock?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);

    // Exclude stock belonging to inactive products.
    Task<(IReadOnlyList<Stock> Items, int Total)> ListByBranchAsync(
        Guid branchId, int skip, int take, CancellationToken cancellationToken);

    Task<(IReadOnlyList<Stock> Items, int Total)> ListByProductAsync(
        Guid productId, int skip, int take, CancellationToken cancellationToken);

    // Active-product stock below MinimumStock, ordered by descending shortage.
    Task<(IReadOnlyList<LowStockDetail> Items, int Total)> ListLowStockAsync(
        Guid? branchId, int skip, int take, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid productId, Guid branchId, CancellationToken cancellationToken);

    Task AddAsync(Stock stock, CancellationToken cancellationToken);

    void Delete(Stock stock);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
