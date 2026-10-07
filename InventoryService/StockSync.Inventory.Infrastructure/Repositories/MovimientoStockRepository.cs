using Microsoft.EntityFrameworkCore;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Repositories;

namespace StockSync.Inventory.Infrastructure.Repositories;

public class MovimientoStockRepository : IMovimientoStockRepository
{
    private readonly InventoryDbContext _context;

    public MovimientoStockRepository(InventoryDbContext context)
    {
        _context = context;
    }

    public async Task AgregarAsync(MovimientoStock movimiento, CancellationToken cancellationToken) =>
        await _context.MovimientosStock.AddAsync(movimiento, cancellationToken);

    public Task<MovimientoStock?> ObtenerPorIdAsync(Guid stockId, Guid id, CancellationToken cancellationToken) =>
        _context.MovimientosStock.AsNoTracking()
            .FirstOrDefaultAsync(m => m.StockId == stockId && m.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<MovimientoStock> Items, int Total)> ListarAsync(
        Guid stockId, int skip, int take, CancellationToken cancellationToken)
    {
        var query = _context.MovimientosStock.AsNoTracking().Where(m => m.StockId == stockId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(m => m.Fecha).ThenByDescending(m => m.Id)
            .Skip(skip).Take(take).ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<(IReadOnlyList<MovimientoStockDetalle> Items, int Total)> ListarGeneralAsync(
        int skip, int take, CancellationToken cancellationToken)
    {
        // El historial incluye productos dados de baja. Nombre y SKU reflejan los datos actuales.
        var query = from movimiento in _context.MovimientosStock.AsNoTracking()
                    join stock in _context.Stocks.AsNoTracking() on movimiento.StockId equals stock.Id
                    join producto in _context.Productos.AsNoTracking() on stock.ProductoId equals producto.Id
                    select new { Movimiento = movimiento, Stock = stock, Producto = producto };

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.Movimiento.Fecha).ThenByDescending(x => x.Movimiento.Id)
            .Skip(skip).Take(take)
            .Select(x => new MovimientoStockDetalle(x.Movimiento, x.Producto.Id,
                x.Producto.Nombre, x.Producto.Sku, x.Stock.SucursalId))
            .ToListAsync(cancellationToken);
        return (items, total);
    }
}
