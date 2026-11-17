using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockSync.Orders.Application.Common.Exceptions;
using StockSync.Orders.Domain.Entities;
using StockSync.Orders.Domain.Repositories;

namespace StockSync.Orders.Infrastructure.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly OrdersDbContext _context;

    public OrderRepository(OrdersDbContext context)
    {
        _context = context;
    }

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Orders
            .AsNoTracking()
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    // The tenant filter is applied around the raw query, so another tenant's order is not found.
    public Task<Order?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Orders
            .FromSqlInterpolated($"SELECT * FROM \"Orders\" WHERE \"Id\" = {id} FOR UPDATE")
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<(IReadOnlyList<Order> Items, int Total)> ListAsync(
        OrderStatus? status,
        Guid? branchId,
        DateTime? from,
        DateTime? to,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query = _context.Orders.AsNoTracking();

        if (status.HasValue)
            query = query.Where(o => o.Status == status);
        if (branchId.HasValue)
            query = query.Where(o => o.BranchId == branchId);
        if (from.HasValue)
            query = query.Where(o => o.CreatedAt >= from);
        if (to.HasValue)
            query = query.Where(o => o.CreatedAt <= to);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .ThenBy(o => o.Id)
            .Skip(skip)
            .Take(take)
            .Include(o => o.Lines)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task AddAsync(Order order, CancellationToken cancellationToken) =>
        await _context.Orders.AddAsync(order, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The order changed during the operation. Reload it and try again.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("The product is already in the order; update the line quantity instead.");
        }
    }
}
