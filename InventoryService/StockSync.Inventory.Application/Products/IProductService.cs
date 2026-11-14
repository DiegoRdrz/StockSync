using StockSync.Inventory.Application.Common;

namespace StockSync.Inventory.Application.Products;

public interface IProductService
{
    Task<ProductResponse> CreateAsync(ProductRequest request, CancellationToken cancellationToken);

    Task<ProductResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<ProductResponse>> ListAsync(ProductFilter filter, CancellationToken cancellationToken);

    Task<PagedResult<ProductResponse>> ListByCategoryAsync(
        Guid categoryId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<ProductResponse> UpdateAsync(Guid id, ProductRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
