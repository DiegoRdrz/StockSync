using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockSync.Inventory.Application.Common.Exceptions;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Repositories;

namespace StockSync.Inventory.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly InventoryDbContext _context;

    public ProductRepository(InventoryDbContext context)
    {
        _context = context;
    }

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive, cancellationToken);

    public Task<Product?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Products
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive, cancellationToken);

    public async Task<(IReadOnlyList<Product> Items, int Total)> ListAsync(
        string? name,
        string? sku,
        Guid? categoryId,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query = _context.Products.AsNoTracking().Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(name))
        {
            var pattern = $"%{EscapeLike(name.Trim())}%";
            query = query.Where(p => EF.Functions.ILike(p.Name, pattern, @"\"));
        }

        if (!string.IsNullOrWhiteSpace(sku))
        {
            var normalizedSku = Product.NormalizeSku(sku);
            query = query.Where(p => p.Sku.Contains(normalizedSku));
        }

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    // Active products only: movement history uses ProductId, not SKU.
    public Task<bool> SkuExistsAsync(string sku, Guid? excludeId, CancellationToken cancellationToken) =>
        _context.Products.AnyAsync(
            p => p.IsActive && p.Sku == sku && (excludeId == null || p.Id != excludeId),
            cancellationToken);

    public Task<bool> HasStockAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Stocks.AnyAsync(s => s.ProductId == id && s.Quantity > 0, cancellationToken);

    public async Task AddAsync(Product product, CancellationToken cancellationToken) =>
        await _context.Products.AddAsync(product, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        // Concurrent creates with the same SKU can both pass the preliminary check.
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("A product with the same SKU already exists.");
        }
    }

    private static string EscapeLike(string value) =>
        value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
