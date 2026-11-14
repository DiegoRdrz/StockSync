using StockSync.Inventory.Application.Common;

namespace StockSync.Inventory.Application.Categories;

public interface ICategoryService
{
    Task<CategoryResponse> CreateAsync(CategoryRequest request, CancellationToken cancellationToken);

    Task<CategoryResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<CategoryResponse>> ListAsync(CategoryFilter filter, CancellationToken cancellationToken);

    Task<CategoryResponse> UpdateAsync(Guid id, CategoryRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
