using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockSync.Inventory.Application.Common.Exceptions;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Repositories;

namespace StockSync.Inventory.Infrastructure.Repositories;

public class StockRepository : IStockRepository
{
    private readonly InventoryDbContext _context;

    public StockRepository(InventoryDbContext context)
    {
        _context = context;
    }

    public Task<Stock?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Stocks
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<Stock?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Stocks
            .FromSqlInterpolated($"SELECT * FROM \"Stocks\" WHERE \"Id\" = {id} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<(IReadOnlyList<Stock> Items, int Total)> ListByBranchAsync(
        Guid branchId, int skip, int take, CancellationToken cancellationToken)
    {
        var query = from stock in _context.Stocks.AsNoTracking()
                    join product in _context.Products.AsNoTracking() on stock.ProductId equals product.Id
                    where stock.BranchId == branchId && product.IsActive
                    select new { Stock = stock, product.Name };

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Stock.Id)
            .Skip(skip)
            .Take(take)
            .Select(x => x.Stock)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<(IReadOnlyList<Stock> Items, int Total)> ListByProductAsync(
        Guid productId, int skip, int take, CancellationToken cancellationToken)
    {
        var query = _context.Stocks.AsNoTracking().Where(s => s.ProductId == productId);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(s => s.BranchId)
            .ThenBy(s => s.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<(IReadOnlyList<LowStockDetail> Items, int Total)> ListLowStockAsync(
        Guid? branchId, int skip, int take, CancellationToken cancellationToken)
    {
        var query = from stock in _context.Stocks.AsNoTracking()
                    join product in _context.Products.AsNoTracking() on stock.ProductId equals product.Id
                    where product.IsActive && stock.Quantity < product.MinimumStock
                    select new { Stock = stock, Product = product };

        if (branchId.HasValue)
            query = query.Where(x => x.Stock.BranchId == branchId);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.Product.MinimumStock - x.Stock.Quantity)
            .ThenBy(x => x.Product.Name)
            .ThenBy(x => x.Stock.Id)
            .Skip(skip)
            .Take(take)
            .Select(x => new LowStockDetail(x.Stock, x.Product.Name, x.Product.Sku, x.Product.MinimumStock))
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<bool> ExistsAsync(Guid productId, Guid branchId, CancellationToken cancellationToken) =>
        _context.Stocks.AnyAsync(
            s => s.ProductId == productId && s.BranchId == branchId,
            cancellationToken);

    public async Task AddAsync(Stock stock, CancellationToken cancellationToken) =>
        await _context.Stocks.AddAsync(stock, cancellationToken);

    public void Delete(Stock stock) =>
        _context.Stocks.Remove(stock);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Stock changed during the operation. Check the current balance and try again.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.ForeignKeyViolation, ConstraintName: "FK_StockMovements_Stocks_StockId" })
        {
            throw new ConflictException("Cannot delete stock with recorded movements, or the stock was deleted during the operation.");
        }
        // Concurrent assignments of the same product and branch can both pass the preliminary check.
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("The product is already assigned to that branch.");
        }
    }
}
