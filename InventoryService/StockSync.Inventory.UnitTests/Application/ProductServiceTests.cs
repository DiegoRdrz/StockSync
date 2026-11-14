using StockSync.Inventory.Application.Common.Exceptions;
using StockSync.Inventory.Application.Products;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Repositories;

namespace StockSync.Inventory.UnitTests.Application;

public class ProductServiceTests
{
    private readonly FakeProductRepository _repository = new();
    private readonly FakeCategoryRepository _categoryRepository = new();
    private readonly ProductService _service;

    public ProductServiceTests()
    {
        _service = new ProductService(_repository, _categoryRepository);
    }

    private static ProductRequest Request(string sku = "FER-001") =>
        new("Hammer", sku, null, 10m, 15m, 5, null);

    [Fact]
    public async Task CreateAsync_ValidRequest_SavesAndReturnsProduct()
    {
        var response = await _service.CreateAsync(Request("fer-001"), CancellationToken.None);

        Assert.Equal("FER-001", response.Sku);
        Assert.Single(_repository.Products);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_SkuDuplicate_ThrowsConflictException()
    {
        await _service.CreateAsync(Request("FER-001"), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(
            () => _service.CreateAsync(Request(" fer-001 "), CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_InvalidRequest_ThrowsValidationExceptionWithoutSaving()
    {
        var request = Request() with { SalePrice = -1m };

        await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(request, CancellationToken.None));
        Assert.Empty(_repository.Products);
    }

    [Fact]
    public async Task GetByIdAsync_Missing_ThrowsNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_SameProductSku_IsValid()
    {
        var created = await _service.CreateAsync(Request("FER-001"), CancellationToken.None);

        var updatedProduct = await _service.UpdateAsync(
            created.Id,
            Request("FER-001") with { Name = "Large hammer" },
            CancellationToken.None);

        Assert.Equal("Large hammer", updatedProduct.Name);
    }

    [Fact]
    public async Task UpdateAsync_AnotherProductSku_ThrowsConflictException()
    {
        await _service.CreateAsync(Request("FER-001"), CancellationToken.None);
        var otherProduct = await _service.CreateAsync(Request("FER-002"), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(
            () => _service.UpdateAsync(otherProduct.Id, Request("FER-001"), CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_DeactivatesAndThenCannotBeFound()
    {
        var created = await _service.CreateAsync(Request(), CancellationToken.None);

        await _service.DeleteAsync(created.Id, CancellationToken.None);

        Assert.False(_repository.Products.Single().IsActive);
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.GetByIdAsync(created.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.DeleteAsync(created.Id, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_WithStock_ThrowsConflictExceptionAndRemainsActive()
    {
        var created = await _service.CreateAsync(Request(), CancellationToken.None);
        _repository.WithStock.Add(created.Id);

        await Assert.ThrowsAsync<ConflictException>(() => _service.DeleteAsync(created.Id, CancellationToken.None));
        Assert.True(_repository.Products.Single().IsActive);
    }

    [Fact]
    public async Task CreateAsync_InactiveProductSku_AllowsReuse()
    {
        var original = await _service.CreateAsync(Request("FER-001"), CancellationToken.None);
        await _service.DeleteAsync(original.Id, CancellationToken.None);

        var newProduct = await _service.CreateAsync(Request(" fer-001 "), CancellationToken.None);

        Assert.NotEqual(original.Id, newProduct.Id);
        Assert.Equal("FER-001", newProduct.Sku);
    }

    [Fact]
    public async Task UpdateAsync_InactiveProductSku_IsValid()
    {
        var inactive = await _service.CreateAsync(Request("FER-001"), CancellationToken.None);
        await _service.DeleteAsync(inactive.Id, CancellationToken.None);
        var otherProduct = await _service.CreateAsync(Request("FER-002"), CancellationToken.None);

        var updatedProduct = await _service.UpdateAsync(otherProduct.Id, Request("FER-001"), CancellationToken.None);

        Assert.Equal("FER-001", updatedProduct.Sku);
    }

    [Fact]
    public async Task ListByCategoryAsync_MissingOrInactiveCategory_ThrowsNotFoundException()
    {
        var inactive = Category.Create("Tools", null);
        inactive.Deactivate();
        _categoryRepository.Categories.Add(inactive);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.ListByCategoryAsync(Guid.NewGuid(), 1, 20, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.ListByCategoryAsync(inactive.Id, 1, 20, CancellationToken.None));
    }

    [Fact]
    public async Task ListByCategoryAsync_ActiveCategory_ReturnsPagedProducts()
    {
        var category = Category.Create("Tools", null);
        _categoryRepository.Categories.Add(category);
        await _service.CreateAsync(Request() with { CategoryId = category.Id }, CancellationToken.None);

        var result = await _service.ListByCategoryAsync(category.Id, 1, 20, CancellationToken.None);

        Assert.Equal(1, result.Total);
        await Assert.ThrowsAsync<ValidationException>(
            () => _service.ListByCategoryAsync(category.Id, 0, 20, CancellationToken.None));
    }

    [Fact]
    public async Task ListAsync_InvalidFilter_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _service.ListAsync(new ProductFilter { Page = 0 }, CancellationToken.None));
    }

    [Fact]
    public async Task ListAsync_CalculatesPagination()
    {
        for (var i = 0; i < 5; i++)
            await _service.CreateAsync(Request($"FER-00{i}"), CancellationToken.None);

        var result = await _service.ListAsync(
            new ProductFilter { Page = 2, PageSize = 2 },
            CancellationToken.None);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(5, result.Total);
        Assert.Equal(3, result.TotalPages);
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        public List<Product> Products { get; } = [];
        public int SaveCount { get; private set; }

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Products.FirstOrDefault(p => p.Id == id && p.IsActive));

        public Task<Product?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
            GetByIdAsync(id, cancellationToken);

        public Task<(IReadOnlyList<Product> Items, int Total)> ListAsync(
            string? name,
            string? sku,
            Guid? categoryId,
            int skip,
            int take,
            CancellationToken cancellationToken)
        {
            var activeItems = Products.Where(p => p.IsActive).ToList();
            IReadOnlyList<Product> items = activeItems.Skip(skip).Take(take).ToList();
            return Task.FromResult((items, activeItems.Count));
        }

        public Task<bool> SkuExistsAsync(string sku, Guid? excludeId, CancellationToken cancellationToken) =>
            Task.FromResult(Products.Any(p => p.IsActive && p.Sku == sku && p.Id != excludeId));

        public HashSet<Guid> WithStock { get; } = [];

        public Task<bool> HasStockAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(WithStock.Contains(id));

        public Task AddAsync(Product product, CancellationToken cancellationToken)
        {
            Products.Add(product);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCategoryRepository : ICategoryRepository
    {
        public List<Category> Categories { get; } = [];

        public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Categories.FirstOrDefault(c => c.Id == id && c.IsActive));

        public Task<Category?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
            GetByIdAsync(id, cancellationToken);

        public Task<(IReadOnlyList<Category> Items, int Total)> ListAsync(
            string? name,
            int skip,
            int take,
            CancellationToken cancellationToken)
        {
            var activeCategories = Categories.Where(c => c.IsActive).ToList();
            IReadOnlyList<Category> items = activeCategories.Skip(skip).Take(take).ToList();
            return Task.FromResult((items, activeCategories.Count));
        }

        public Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken) =>
            Task.FromResult(Categories.Any(c => Category.NormalizeName(c.Name) == name && c.Id != excludeId));

        public Task<bool> ActiveExistsAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Categories.Any(c => c.Id == id && c.IsActive));

        public Task<bool> HasActiveProductsAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task AddAsync(Category category, CancellationToken cancellationToken)
        {
            Categories.Add(category);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
