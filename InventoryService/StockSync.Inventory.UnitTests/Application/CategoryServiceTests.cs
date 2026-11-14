using StockSync.Inventory.Application.Categories;
using StockSync.Inventory.Application.Common.Exceptions;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Repositories;

namespace StockSync.Inventory.UnitTests.Application;

public class CategoryServiceTests
{
    private readonly FakeCategoryRepository _repository = new();
    private readonly CategoryService _service;

    public CategoryServiceTests()
    {
        _service = new CategoryService(_repository);
    }

    [Fact]
    public async Task CreateAsync_ValidCategory_SavesAndReturnsResponse()
    {
        var response = await _service.CreateAsync(new("Tools", null), CancellationToken.None);

        Assert.Equal("Tools", response.Name);
        Assert.Single(_repository.Categories);
    }

    [Fact]
    public async Task CreateAsync_NameDuplicate_ThrowsConflictException()
    {
        await _service.CreateAsync(new("Tools", null), CancellationToken.None);

        // Ignore case and surrounding whitespace.
        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.CreateAsync(new(" tools ", null), CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_InactiveCategoryName_AllowsRecreation()
    {
        var original = await _service.CreateAsync(new("Chocodsa", null), CancellationToken.None);
        await _service.DeleteAsync(original.Id, CancellationToken.None);

        var recreated = await _service.CreateAsync(new(" chocodsa ", null), CancellationToken.None);

        Assert.NotEqual(original.Id, recreated.Id);
        Assert.True(recreated.IsActive);
    }

    [Fact]
    public async Task UpdateAsync_InactiveCategoryName_IsValid()
    {
        var inactive = await _service.CreateAsync(new("Temporary", null), CancellationToken.None);
        await _service.DeleteAsync(inactive.Id, CancellationToken.None);
        var otherCategory = await _service.CreateAsync(new("Tools", null), CancellationToken.None);

        var updatedCategory = await _service.UpdateAsync(otherCategory.Id, new("Temporary", null), CancellationToken.None);

        Assert.Equal("Temporary", updatedCategory.Name);
    }

    [Fact]
    public async Task DeleteAsync_WithActiveProducts_ThrowsConflictException()
    {
        var category = await _service.CreateAsync(new("Tools", null), CancellationToken.None);
        _repository.SimulateActiveProducts(true);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.DeleteAsync(category.Id, CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdAsync_Missing_ThrowsNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None));
    }

    private sealed class FakeCategoryRepository : ICategoryRepository
    {
        public List<Category> Categories { get; } = [];
        private bool _hasActiveProducts = false;

        public void SimulateActiveProducts(bool hasProducts) => _hasActiveProducts = hasProducts;

        public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Categories.FirstOrDefault(c => c.Id == id && c.IsActive));

        public Task<Category?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
            GetByIdAsync(id, cancellationToken);

        public Task<(IReadOnlyList<Category> Items, int Total)> ListAsync(string? name, int skip, int take, CancellationToken cancellationToken) =>
            Task.FromResult(((IReadOnlyList<Category>)Categories.Where(c => c.IsActive).ToList(), Categories.Count(c => c.IsActive)));

        public Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken) =>
            Task.FromResult(Categories.Any(c => c.IsActive && Category.NormalizeName(c.Name) == name && c.Id != excludeId));

        public Task<bool> ActiveExistsAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Categories.Any(c => c.Id == id && c.IsActive));

        public Task<bool> HasActiveProductsAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(_hasActiveProducts);

        public Task AddAsync(Category category, CancellationToken cancellationToken)
        {
            Categories.Add(category);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
