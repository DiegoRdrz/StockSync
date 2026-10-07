using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockSync.Inventory.Application.Common.Exceptions;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Repositories;

namespace StockSync.Inventory.Infrastructure.Repositories;

public class ProductoRepository : IProductoRepository
{
    private readonly InventoryDbContext _context;

    public ProductoRepository(InventoryDbContext context)
    {
        _context = context;
    }

    public Task<Producto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Productos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.Activo, cancellationToken);

    public Task<Producto?> ObtenerParaActualizarAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Productos
            .FirstOrDefaultAsync(p => p.Id == id && p.Activo, cancellationToken);

    public async Task<(IReadOnlyList<Producto> Items, int Total)> ListarAsync(
        string? nombre,
        string? sku,
        Guid? categoriaId,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query = _context.Productos.AsNoTracking().Where(p => p.Activo);

        if (!string.IsNullOrWhiteSpace(nombre))
        {
            var patron = $"%{EscaparLike(nombre.Trim())}%";
            query = query.Where(p => EF.Functions.ILike(p.Nombre, patron, @"\"));
        }

        if (!string.IsNullOrWhiteSpace(sku))
        {
            var skuNormalizado = Producto.NormalizarSku(sku);
            query = query.Where(p => p.Sku.Contains(skuNormalizado));
        }

        if (categoriaId.HasValue)
            query = query.Where(p => p.CategoriaId == categoriaId);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(p => p.Nombre)
            .ThenBy(p => p.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    // Solo productos activos: el historial de movimientos se vincula por ProductoId, no por SKU.
    public Task<bool> ExisteSkuAsync(string sku, Guid? excluirId, CancellationToken cancellationToken) =>
        _context.Productos.AnyAsync(
            p => p.Activo && p.Sku == sku && (excluirId == null || p.Id != excluirId),
            cancellationToken);

    public async Task AgregarAsync(Producto producto, CancellationToken cancellationToken) =>
        await _context.Productos.AddAsync(producto, cancellationToken);

    public async Task GuardarCambiosAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        // Dos altas concurrentes con el mismo SKU pueden pasar ambas la verificación previa.
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("Ya existe un producto con el mismo SKU.");
        }
    }

    private static string EscaparLike(string valor) =>
        valor.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
