using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockSync.Orders.Application.Common.Exceptions;
using StockSync.Orders.Domain.Entities;
using StockSync.Orders.Domain.Repositories;

namespace StockSync.Orders.Infrastructure.Repositories;

public class PurchaseOrderRepository : IPurchaseOrderRepository
{
    private readonly OrdersDbContext _context;

    public PurchaseOrderRepository(OrdersDbContext context)
    {
        _context = context;
    }

    public Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _context.PurchaseOrders
            .AsNoTracking()
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    // The tenant filter is applied around the raw query, so another tenant's purchase order is not found.
    public Task<PurchaseOrder?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        _context.PurchaseOrders
            .FromSqlInterpolated($"SELECT * FROM \"PurchaseOrders\" WHERE \"Id\" = {id} FOR UPDATE")
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<(IReadOnlyList<PurchaseOrder> Items, int Total)> ListAsync(
        PurchaseOrderStatus? status,
        Guid? branchId,
        DateTime? from,
        DateTime? to,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query = _context.PurchaseOrders.AsNoTracking();

        if (status.HasValue)
            query = query.Where(p => p.Status == status);
        if (branchId.HasValue)
            query = query.Where(p => p.BranchId == branchId);
        if (from.HasValue)
            query = query.Where(p => p.CreatedAt >= from);
        if (to.HasValue)
            query = query.Where(p => p.CreatedAt <= to);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Skip(skip)
            .Take(take)
            .Include(p => p.Lines)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task AddAsync(PurchaseOrder purchaseOrder, CancellationToken cancellationToken) =>
        await _context.PurchaseOrders.AddAsync(purchaseOrder, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The purchase order changed during the operation. Reload it and try again.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("The product is already in the purchase order; update the line quantity instead.");
        }
    }
}
