using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Domain.Repositories;

public interface IStockMovementRepository
{
    // Share the stock context; stock SaveChangesAsync commits both records atomically.
    Task AddAsync(StockMovement movement, CancellationToken cancellationToken);

    Task<StockMovement?> GetByIdAsync(Guid stockId, Guid id, CancellationToken cancellationToken);

    Task<(IReadOnlyList<StockMovement> Items, int Total)> ListAsync(
        Guid stockId, int skip, int take, CancellationToken cancellationToken);

    Task<(IReadOnlyList<StockMovementDetail> Items, int Total)> ListAllAsync(
        int skip, int take, CancellationToken cancellationToken);
}
