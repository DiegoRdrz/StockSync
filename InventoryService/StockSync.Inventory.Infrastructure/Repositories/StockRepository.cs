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
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<Stock?> ObtenerPorProductoYSucursalAsync(Guid productoId, Guid sucursalId, CancellationToken cancellationToken) =>
        _context.Stocks
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ProductoId == productoId && s.SucursalId == sucursalId, cancellationToken);

    public async Task<IReadOnlyList<Stock>> ListarPorSucursalAsync(Guid sucursalId, CancellationToken cancellationToken) =>
        await _context.Stocks
            .AsNoTracking()
            .Where(s => s.SucursalId == sucursalId)
            .OrderBy(s => s.ProductoId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Stock>> ListarPorProductoAsync(Guid productoId, CancellationToken cancellationToken) =>
        await _context.Stocks
            .AsNoTracking()
            .Where(s => s.ProductoId == productoId)
            .OrderBy(s => s.SucursalId)
            .ToListAsync(cancellationToken);

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
        // Dos altas concurrentes del mismo producto en la misma sucursal pueden pasar ambas la verificación previa.
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("El producto ya está asignado a esa sucursal.");
        }
    }
}
