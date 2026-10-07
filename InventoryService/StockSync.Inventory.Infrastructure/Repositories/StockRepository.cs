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

    public Task<Stock?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Stocks
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<Stock?> ObtenerParaActualizarAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Stocks
            .FromSqlInterpolated($"SELECT * FROM \"Stocks\" WHERE \"Id\" = {id} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<(IReadOnlyList<Stock> Items, int Total)> ListarPorSucursalAsync(
        Guid sucursalId, int skip, int take, CancellationToken cancellationToken)
    {
        var query = from stock in _context.Stocks.AsNoTracking()
                    join producto in _context.Productos.AsNoTracking() on stock.ProductoId equals producto.Id
                    where stock.SucursalId == sucursalId && producto.Activo
                    select new { Stock = stock, producto.Nombre };

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.Nombre)
            .ThenBy(x => x.Stock.Id)
            .Skip(skip)
            .Take(take)
            .Select(x => x.Stock)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<(IReadOnlyList<Stock> Items, int Total)> ListarPorProductoAsync(
        Guid productoId, int skip, int take, CancellationToken cancellationToken)
    {
        var query = _context.Stocks.AsNoTracking().Where(s => s.ProductoId == productoId);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(s => s.SucursalId)
            .ThenBy(s => s.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<(IReadOnlyList<StockBajoMinimoDetalle> Items, int Total)> ListarBajoMinimoAsync(
        Guid? sucursalId, int skip, int take, CancellationToken cancellationToken)
    {
        var query = from stock in _context.Stocks.AsNoTracking()
                    join producto in _context.Productos.AsNoTracking() on stock.ProductoId equals producto.Id
                    where producto.Activo && stock.Cantidad < producto.StockMinimo
                    select new { Stock = stock, Producto = producto };

        if (sucursalId.HasValue)
            query = query.Where(x => x.Stock.SucursalId == sucursalId);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.Producto.StockMinimo - x.Stock.Cantidad)
            .ThenBy(x => x.Producto.Nombre)
            .ThenBy(x => x.Stock.Id)
            .Skip(skip)
            .Take(take)
            .Select(x => new StockBajoMinimoDetalle(x.Stock, x.Producto.Nombre, x.Producto.Sku, x.Producto.StockMinimo))
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<bool> ExisteAsync(Guid productoId, Guid sucursalId, CancellationToken cancellationToken) =>
        _context.Stocks.AnyAsync(
            s => s.ProductoId == productoId && s.SucursalId == sucursalId,
            cancellationToken);

    public async Task AgregarAsync(Stock stock, CancellationToken cancellationToken) =>
        await _context.Stocks.AddAsync(stock, cancellationToken);

    public void Eliminar(Stock stock) =>
        _context.Stocks.Remove(stock);

    public async Task GuardarCambiosAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("El stock cambió durante la operación. Consulte el saldo actual y vuelva a intentarlo.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.ForeignKeyViolation, ConstraintName: "FK_MovimientosStock_Stocks_StockId" })
        {
            throw new ConflictException("No se puede eliminar un stock con movimientos registrados, o el stock fue eliminado durante la operación.");
        }
        // Dos altas concurrentes del mismo producto en la misma sucursal pueden pasar ambas la verificación previa.
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("El producto ya está asignado a esa sucursal.");
        }
    }
}
