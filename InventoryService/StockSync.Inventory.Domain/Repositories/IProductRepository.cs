using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Domain.Repositories;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Product?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<(IReadOnlyList<Product> Items, int Total)> ListAsync(
        string? name,
        string? sku,
        Guid? categoryId,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<bool> SkuExistsAsync(string sku, Guid? excludeId, CancellationToken cancellationToken);

    Task<bool> HasStockAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(Product product, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
