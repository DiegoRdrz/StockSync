using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Domain.Repositories;

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Category?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<(IReadOnlyList<Category> Items, int Total)> ListAsync(
        string? name,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken);

    Task<bool> ActiveExistsAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> HasActiveProductsAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(Category category, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
