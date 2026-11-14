using Microsoft.EntityFrameworkCore;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Repositories;

namespace StockSync.Inventory.Infrastructure.Repositories;

public class StockMovementRepository : IStockMovementRepository
{
    private readonly InventoryDbContext _context;

    public StockMovementRepository(InventoryDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(StockMovement movement, CancellationToken cancellationToken) =>
        await _context.StockMovements.AddAsync(movement, cancellationToken);

    public Task<StockMovement?> GetByIdAsync(Guid stockId, Guid id, CancellationToken cancellationToken) =>
        _context.StockMovements.AsNoTracking()
            .FirstOrDefaultAsync(m => m.StockId == stockId && m.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<StockMovement> Items, int Total)> ListAsync(
        Guid stockId, int skip, int take, CancellationToken cancellationToken)
    {
        var query = _context.StockMovements.AsNoTracking().Where(m => m.StockId == stockId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(m => m.Date).ThenByDescending(m => m.Id)
            .Skip(skip).Take(take).ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<(IReadOnlyList<StockMovementDetail> Items, int Total)> ListAllAsync(
        int skip, int take, CancellationToken cancellationToken)
    {
        // History includes inactive products; names and SKUs reflect current product data.
        var query = from movement in _context.StockMovements.AsNoTracking()
                    join stock in _context.Stocks.AsNoTracking() on movement.StockId equals stock.Id
                    join product in _context.Products.AsNoTracking() on stock.ProductId equals product.Id
                    select new { Movement = movement, Stock = stock, Product = product };

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.Movement.Date).ThenByDescending(x => x.Movement.Id)
            .Skip(skip).Take(take)
            .Select(x => new StockMovementDetail(x.Movement, x.Product.Id,
                x.Product.Name, x.Product.Sku, x.Stock.BranchId))
            .ToListAsync(cancellationToken);
        return (items, total);
    }
}
