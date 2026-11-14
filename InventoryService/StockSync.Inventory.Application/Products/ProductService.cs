using StockSync.Inventory.Application.Common;
using StockSync.Inventory.Application.Common.Exceptions;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Repositories;

namespace StockSync.Inventory.Application.Products;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;

    public ProductService(IProductRepository productRepository, ICategoryRepository categoryRepository)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
    }

    public async Task<ProductResponse> CreateAsync(ProductRequest request, CancellationToken cancellationToken)
    {
        ThrowIfErrors(ProductValidator.Validate(request));
        await EnsureValidCategoryAsync(request.CategoryId, cancellationToken);
        await EnsureSkuAvailableAsync(request.Sku, null, cancellationToken);

        var product = Product.Create(
            request.Name,
            request.Sku,
            request.Description,
            request.PurchasePrice,
            request.SalePrice,
            request.MinimumStock,
            request.CategoryId);

        await _productRepository.AddAsync(product, cancellationToken);
        await _productRepository.SaveChangesAsync(cancellationToken);

        return ProductResponse.From(product);
    }

    public async Task<ProductResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken)
            ?? throw ProductNotFound(id);

        return ProductResponse.From(product);
    }

    public async Task<PagedResult<ProductResponse>> ListAsync(ProductFilter filter, CancellationToken cancellationToken)
    {
        ThrowIfErrors(ProductValidator.Validate(filter));

        var (items, total) = await _productRepository.ListAsync(
            filter.Name,
            filter.Sku,
            filter.CategoryId,
            (filter.Page - 1) * filter.PageSize,
            filter.PageSize,
            cancellationToken);

        return new PagedResult<ProductResponse>(
            items.Select(ProductResponse.From).ToList(),
            filter.Page,
            filter.PageSize,
            total);
    }

    public async Task<PagedResult<ProductResponse>> ListByCategoryAsync(
        Guid categoryId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (!await _categoryRepository.ActiveExistsAsync(categoryId, cancellationToken))
            throw new NotFoundException($"Category '{categoryId}' was not found.");

        var filter = new ProductFilter { CategoryId = categoryId, Page = page, PageSize = pageSize };
        return await ListAsync(filter, cancellationToken);
    }

    public async Task<ProductResponse> UpdateAsync(Guid id, ProductRequest request, CancellationToken cancellationToken)
    {
        ThrowIfErrors(ProductValidator.Validate(request));

        var product = await _productRepository.GetForUpdateAsync(id, cancellationToken)
            ?? throw ProductNotFound(id);

        await EnsureValidCategoryAsync(request.CategoryId, cancellationToken);
        await EnsureSkuAvailableAsync(request.Sku, id, cancellationToken);

        product.Update(
            request.Name,
            request.Sku,
            request.Description,
            request.PurchasePrice,
            request.SalePrice,
            request.MinimumStock,
            request.CategoryId);

        await _productRepository.SaveChangesAsync(cancellationToken);

        return ProductResponse.From(product);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetForUpdateAsync(id, cancellationToken)
            ?? throw ProductNotFound(id);

        // Block deletion to avoid leaving stock units without a product.
        if (await _productRepository.HasStockAsync(id, cancellationToken))
            throw new ConflictException("Cannot delete the product because it has stock in one or more branches.");

        product.Deactivate();
        await _productRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureValidCategoryAsync(Guid? categoryId, CancellationToken cancellationToken)
    {
        if (categoryId is null)
            return;

        if (!await _categoryRepository.ActiveExistsAsync(categoryId.Value, cancellationToken))
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(ProductRequest.CategoryId)] = [$"Category '{categoryId}' does not exist or is inactive."]
            });
    }

    private async Task EnsureSkuAvailableAsync(string sku, Guid? excludeId, CancellationToken cancellationToken)
    {
        var normalizedSku = Product.NormalizeSku(sku);
        if (await _productRepository.SkuExistsAsync(normalizedSku, excludeId, cancellationToken))
            throw new ConflictException($"A product with SKU '{normalizedSku}' already exists.");
    }

    private static void ThrowIfErrors(Dictionary<string, string[]> errors)
    {
        if (errors.Count > 0)
            throw new ValidationException(errors);
    }

    private static NotFoundException ProductNotFound(Guid id) =>
        new($"Product '{id}' was not found.");
}
