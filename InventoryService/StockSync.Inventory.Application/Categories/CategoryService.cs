using StockSync.Inventory.Application.Common;
using StockSync.Inventory.Application.Common.Exceptions;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Repositories;

namespace StockSync.Inventory.Application.Categories;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categoryRepository;

    public CategoryService(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<CategoryResponse> CreateAsync(CategoryRequest request, CancellationToken cancellationToken)
    {
        ThrowIfErrors(CategoryValidator.Validate(request));
        await EnsureNameAvailableAsync(request.Name, null, cancellationToken);

        var category = Category.Create(request.Name, request.Description);

        await _categoryRepository.AddAsync(category, cancellationToken);
        await _categoryRepository.SaveChangesAsync(cancellationToken);

        return CategoryResponse.From(category);
    }

    public async Task<CategoryResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(id, cancellationToken)
            ?? throw CategoryNotFound(id);

        return CategoryResponse.From(category);
    }

    public async Task<PagedResult<CategoryResponse>> ListAsync(CategoryFilter filter, CancellationToken cancellationToken)
    {
        ThrowIfErrors(CategoryValidator.Validate(filter));

        var (items, total) = await _categoryRepository.ListAsync(
            filter.Name,
            (filter.Page - 1) * filter.PageSize,
            filter.PageSize,
            cancellationToken);

        return new PagedResult<CategoryResponse>(
            items.Select(CategoryResponse.From).ToList(),
            filter.Page,
            filter.PageSize,
            total);
    }

    public async Task<CategoryResponse> UpdateAsync(Guid id, CategoryRequest request, CancellationToken cancellationToken)
    {
        ThrowIfErrors(CategoryValidator.Validate(request));

        var category = await _categoryRepository.GetForUpdateAsync(id, cancellationToken)
            ?? throw CategoryNotFound(id);

        await EnsureNameAvailableAsync(request.Name, id, cancellationToken);

        category.Update(request.Name, request.Description);
        await _categoryRepository.SaveChangesAsync(cancellationToken);

        return CategoryResponse.From(category);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetForUpdateAsync(id, cancellationToken)
            ?? throw CategoryNotFound(id);

        // Block deletion rather than silently leaving products uncategorized.
        if (await _categoryRepository.HasActiveProductsAsync(id, cancellationToken))
            throw new ConflictException("Cannot delete the category because it has active products.");

        category.Deactivate();
        await _categoryRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureNameAvailableAsync(string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        var normalizedName = Category.NormalizeName(name);
        if (await _categoryRepository.NameExistsAsync(normalizedName, excludeId, cancellationToken))
            throw new ConflictException($"A category named '{name.Trim()}' already exists.");
    }

    private static void ThrowIfErrors(Dictionary<string, string[]> errors)
    {
        if (errors.Count > 0)
            throw new ValidationException(errors);
    }

    private static NotFoundException CategoryNotFound(Guid id) =>
        new($"Category '{id}' was not found.");
}
