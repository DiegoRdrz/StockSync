using StockSync.Orders.Domain.Entities;

namespace StockSync.Orders.Domain.Repositories;

public interface IPurchaseOrderRepository
{
    // Read-only copy with its lines.
    Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    // Tracked copy with its lines. Lock the row until the current transaction ends (see IUnitOfWork).
    Task<PurchaseOrder?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);

    // Ordered by descending creation date; the date range applies to CreatedAt.
    Task<(IReadOnlyList<PurchaseOrder> Items, int Total)> ListAsync(
        PurchaseOrderStatus? status,
        Guid? branchId,
        DateTime? from,
        DateTime? to,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task AddAsync(PurchaseOrder purchaseOrder, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
