using StockSync.Orders.Domain.Entities;

namespace StockSync.Orders.Domain.Repositories;

public interface IOrderRepository
{
    // Read-only copy with its lines.
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    // Tracked copy with its lines. Lock the row until the current transaction ends (see IUnitOfWork).
    Task<Order?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);

    // Ordered by descending creation date; the date range applies to CreatedAt.
    Task<(IReadOnlyList<Order> Items, int Total)> ListAsync(
        OrderStatus? status,
        Guid? branchId,
        DateTime? from,
        DateTime? to,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task AddAsync(Order order, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
