using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockSync.Inventory.Application.Common.Exceptions;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Repositories;

namespace StockSync.Inventory.Infrastructure.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly InventoryDbContext _context;

    public CategoryRepository(InventoryDbContext context)
    {
        _context = context;
    }

    public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive, cancellationToken);

    public Task<Category?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Categories
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive, cancellationToken);

    public async Task<(IReadOnlyList<Category> Items, int Total)> ListAsync(
        string? name,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query = _context.Categories.AsNoTracking().Where(c => c.IsActive);

        if (!string.IsNullOrWhiteSpace(name))
        {
            var pattern = $"%{EscapeLike(name.Trim())}%";
            query = query.Where(c => EF.Functions.ILike(c.Name, pattern, @"\"));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(c => c.Name)
            .ThenBy(c => c.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken) =>
        _context.Categories.AnyAsync(
            c => c.IsActive && c.NormalizedName == name && (excludeId == null || c.Id != excludeId),
            cancellationToken);

    public Task<bool> ActiveExistsAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Categories.AnyAsync(c => c.Id == id && c.IsActive, cancellationToken);

    public Task<bool> HasActiveProductsAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Products.AnyAsync(p => p.CategoryId == id && p.IsActive, cancellationToken);

    public async Task AddAsync(Category category, CancellationToken cancellationToken) =>
        await _context.Categories.AddAsync(category, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("A category with the same name already exists.");
        }
    }

    private static string EscapeLike(string value) =>
        value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
