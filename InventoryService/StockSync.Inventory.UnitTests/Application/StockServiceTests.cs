using StockSync.Inventory.Application.Common.Exceptions;
using StockSync.Inventory.Application.Stocks;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Repositories;

namespace StockSync.Inventory.UnitTests.Application;

public partial class StockServiceTests
{
    private readonly FakeStockRepository _stockRepository = new();
    private readonly FakeProductRepository _productRepository = new();
    private readonly InMemoryUnitOfWork _unitOfWork = new();
    private readonly StockService _service;

    public StockServiceTests()
    {
        _service = new StockService(_stockRepository, _productRepository, _movements, _unitOfWork);
    }

    private Product CreateProduct(string sku = "FER-001")
    {
        var product = Product.Create("Hammer", sku, null, 10m, 15m, 5, null);
        _productRepository.Products.Add(product);
        return product;
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_SavesAndReturnsStock()
    {
        var product = CreateProduct();
        var branchId = Guid.NewGuid();

        var response = await _service.CreateAsync(new StockRequest(product.Id, branchId, 20), CancellationToken.None);

        Assert.Equal(product.Id, response.ProductId);
        Assert.Equal(branchId, response.BranchId);
        Assert.Equal(20, response.Quantity);
        Assert.Single(_stockRepository.Stocks);
        Assert.Equal(1, _stockRepository.SaveCount);
        var initialBalance = Assert.Single(_movements.Items);
        Assert.Equal(StockMovementType.Adjustment, initialBalance.Type);
        Assert.Equal((0, 20), (initialBalance.PreviousQuantity, initialBalance.NewQuantity));
    }

    [Fact]
    public async Task CreateAsync_ZeroInitialBalance_DoesNotRecordMovement()
    {
        var product = CreateProduct();

        await _service.CreateAsync(new StockRequest(product.Id, Guid.NewGuid(), 0), CancellationToken.None);

        Assert.Empty(_movements.Items);
    }

    [Fact]
    public async Task CreateAsync_MissingProduct_ThrowsValidationExceptionWithoutSaving()
    {
        var request = new StockRequest(Guid.NewGuid(), Guid.NewGuid(), 20);

        await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(request, CancellationToken.None));
        Assert.Empty(_stockRepository.Stocks);
    }

    [Fact]
    public async Task CreateAsync_InactiveProduct_ThrowsValidationException()
    {
        var product = CreateProduct();
        product.Deactivate();

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateAsync(new StockRequest(product.Id, Guid.NewGuid(), 20), CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_DuplicateAssignment_ThrowsConflictException()
    {
        var product = CreateProduct();
        var branchId = Guid.NewGuid();
        await _service.CreateAsync(new StockRequest(product.Id, branchId, 20), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(
            () => _service.CreateAsync(new StockRequest(product.Id, branchId, 5), CancellationToken.None));
        Assert.Single(_stockRepository.Stocks);
    }

    [Fact]
    public async Task CreateAsync_SameProductInAnotherBranch_IsValid()
    {
        var product = CreateProduct();
        await _service.CreateAsync(new StockRequest(product.Id, Guid.NewGuid(), 20), CancellationToken.None);

        await _service.CreateAsync(new StockRequest(product.Id, Guid.NewGuid(), 8), CancellationToken.None);

        Assert.Equal(2, _stockRepository.Stocks.Count);
    }

    [Fact]
    public async Task CreateAsync_InvalidRequest_ThrowsValidationExceptionWithoutSaving()
    {
        var product = CreateProduct();
        var request = new StockRequest(product.Id, Guid.Empty, -1);

        await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(request, CancellationToken.None));
        Assert.Empty(_stockRepository.Stocks);
        Assert.Equal(0, _stockRepository.SaveCount);
    }

    [Fact]
    public async Task GetByIdAsync_Existing_ReturnsStock()
    {
        var product = CreateProduct();
        var created = await _service.CreateAsync(new StockRequest(product.Id, Guid.NewGuid(), 20), CancellationToken.None);

        var response = await _service.GetByIdAsync(created.Id, CancellationToken.None);

        Assert.Equal(created, response);
    }

    [Fact]
    public async Task GetByIdAsync_Missing_ThrowsNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task ListByBranchAsync_ReturnsOnlyBranchAssignments()
    {
        var product1 = CreateProduct("FER-001");
        var product2 = CreateProduct("FER-002");
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        await _service.CreateAsync(new StockRequest(product1.Id, branchA, 10), CancellationToken.None);
        await _service.CreateAsync(new StockRequest(product2.Id, branchA, 25), CancellationToken.None);
        await _service.CreateAsync(new StockRequest(product1.Id, branchB, 4), CancellationToken.None);

        var result = await _service.ListByBranchAsync(branchA, new StockFilter(), CancellationToken.None);

        Assert.Equal(2, result.Total);
        Assert.All(result.Items, s => Assert.Equal(branchA, s.BranchId));
    }

    [Fact]
    public async Task ListByBranchAsync_WithoutAssignments_ReturnsEmptyList()
    {
        var result = await _service.ListByBranchAsync(Guid.NewGuid(), new StockFilter(), CancellationToken.None);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task ListByBranchAsync_Page()
    {
        var branchId = Guid.NewGuid();
        for (var i = 0; i < 3; i++)
            await _service.CreateAsync(new StockRequest(CreateProduct($"FER-00{i}").Id, branchId, 0), CancellationToken.None);

        var result = await _service.ListByBranchAsync(
            branchId, new StockFilter { Page = 2, PageSize = 2 }, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(3, result.Total);
        Assert.Equal(2, result.TotalPages);
        await Assert.ThrowsAsync<ValidationException>(() => _service.ListByBranchAsync(
            branchId, new StockFilter { Page = int.MaxValue }, CancellationToken.None));
    }

    [Fact]
    public async Task ListByProductAsync_ReturnsOnlyProductAssignments()
    {
        var product1 = CreateProduct("FER-001");
        var product2 = CreateProduct("FER-002");
        await _service.CreateAsync(new StockRequest(product1.Id, Guid.NewGuid(), 10), CancellationToken.None);
        await _service.CreateAsync(new StockRequest(product1.Id, Guid.NewGuid(), 20), CancellationToken.None);
        await _service.CreateAsync(new StockRequest(product2.Id, Guid.NewGuid(), 5), CancellationToken.None);

        var result = await _service.ListByProductAsync(product1.Id, new StockFilter(), CancellationToken.None);

        Assert.Equal(2, result.Total);
        Assert.All(result.Items, s => Assert.Equal(product1.Id, s.ProductId));
    }

    [Fact]
    public async Task ListByProductAsync_MissingOrInactiveProduct_ThrowsNotFoundException()
    {
        var inactive = CreateProduct();
        inactive.Deactivate();

        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.ListByProductAsync(Guid.NewGuid(), new StockFilter(), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.ListByProductAsync(inactive.Id, new StockFilter(), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateQuantityAsync_ValidQuantity_UpdatesOnlyQuantity()
    {
        var product = CreateProduct();
        var branchId = Guid.NewGuid();
        var created = await _service.CreateAsync(new StockRequest(product.Id, branchId, 20), CancellationToken.None);

        var updatedProduct = await _service.UpdateQuantityAsync(
            created.Id,
            new StockQuantityRequest(3),
            CancellationToken.None);

        Assert.Equal(3, updatedProduct.Quantity);
        Assert.Equal(created.Id, updatedProduct.Id);
        Assert.Equal(product.Id, updatedProduct.ProductId);
        Assert.Equal(branchId, updatedProduct.BranchId);
        Assert.Equal(2, _stockRepository.SaveCount);
        var adjustment = _movements.Items.Last();
        Assert.Equal(StockMovementType.Adjustment, adjustment.Type);
        Assert.Equal((20, 3, 17), (adjustment.PreviousQuantity, adjustment.NewQuantity, adjustment.Quantity));
    }

    [Fact]
    public async Task UpdateQuantityAsync_ZeroQuantity_IsValid()
    {
        var product = CreateProduct();
        var created = await _service.CreateAsync(new StockRequest(product.Id, Guid.NewGuid(), 20), CancellationToken.None);

        var updatedProduct = await _service.UpdateQuantityAsync(created.Id, new StockQuantityRequest(0), CancellationToken.None);

        Assert.Equal(0, updatedProduct.Quantity);
    }

    [Fact]
    public async Task UpdateQuantityAsync_NegativeQuantity_ThrowsValidationExceptionWithoutModification()
    {
        var product = CreateProduct();
        var created = await _service.CreateAsync(new StockRequest(product.Id, Guid.NewGuid(), 20), CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.UpdateQuantityAsync(created.Id, new StockQuantityRequest(-1), CancellationToken.None));
        Assert.Equal(20, _stockRepository.Stocks.Single().Quantity);
    }

    [Fact]
    public async Task UpdateQuantityAsync_Missing_ThrowsNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.UpdateQuantityAsync(Guid.NewGuid(), new StockQuantityRequest(5), CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_DeletesAssignmentButNotProduct()
    {
        var product = CreateProduct();
        var created = await _service.CreateAsync(new StockRequest(product.Id, Guid.NewGuid(), 0), CancellationToken.None);

        await _service.DeleteAsync(created.Id, CancellationToken.None);

        Assert.Empty(_stockRepository.Stocks);
        Assert.True(product.IsActive);
        Assert.Single(_productRepository.Products);
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.GetByIdAsync(created.Id, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_WithStock_ThrowsConflictExceptionWithoutDeleting()
    {
        var product = CreateProduct();
        var created = await _service.CreateAsync(new StockRequest(product.Id, Guid.NewGuid(), 5), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => _service.DeleteAsync(created.Id, CancellationToken.None));
        Assert.Single(_stockRepository.Stocks);
    }

    [Fact]
    public async Task UpdateQuantityAsync_InactiveProduct_ThrowsConflictExceptionWithoutModification()
    {
        var product = CreateProduct();
        var created = await _service.CreateAsync(new StockRequest(product.Id, Guid.NewGuid(), 5), CancellationToken.None);
        product.Deactivate();

        await Assert.ThrowsAsync<ConflictException>(
            () => _service.UpdateQuantityAsync(created.Id, new StockQuantityRequest(0), CancellationToken.None));
        Assert.Equal(5, _stockRepository.Stocks.Single().Quantity);
        Assert.Single(_movements.Items);
    }

    [Fact]
    public async Task DeleteAsync_Missing_ThrowsNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.DeleteAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_AfterDeletingAssignment_AllowsReassignment()
    {
        var product = CreateProduct();
        var branchId = Guid.NewGuid();
        var created = await _service.CreateAsync(new StockRequest(product.Id, branchId, 0), CancellationToken.None);
        await _service.DeleteAsync(created.Id, CancellationToken.None);

        var newProduct = await _service.CreateAsync(new StockRequest(product.Id, branchId, 7), CancellationToken.None);

        Assert.Equal(7, newProduct.Quantity);
    }

    private sealed class FakeStockRepository : IStockRepository
    {
        public List<Stock> Stocks { get; } = [];
        public int SaveCount { get; private set; }

        public Task<Stock?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Stocks.FirstOrDefault(s => s.Id == id));

        public Task<Stock?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
            GetByIdAsync(id, cancellationToken);

        public Task<(IReadOnlyList<Stock> Items, int Total)> ListByBranchAsync(
            Guid branchId, int skip, int take, CancellationToken cancellationToken) =>
            Pager(Stocks.Where(s => s.BranchId == branchId).ToList(), skip, take);

        public Task<(IReadOnlyList<Stock> Items, int Total)> ListByProductAsync(
            Guid productId, int skip, int take, CancellationToken cancellationToken) =>
            Pager(Stocks.Where(s => s.ProductId == productId).ToList(), skip, take);

        // Comparing minimum stock requires a product join, tested against PostgreSQL.
        public Task<(IReadOnlyList<LowStockDetail> Items, int Total)> ListLowStockAsync(
            Guid? branchId, int skip, int take, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        private static Task<(IReadOnlyList<Stock> Items, int Total)> Pager(List<Stock> stocks, int skip, int take) =>
            Task.FromResult<(IReadOnlyList<Stock>, int)>((stocks.Skip(skip).Take(take).ToList(), stocks.Count));

        public Task<bool> ExistsAsync(Guid productId, Guid branchId, CancellationToken cancellationToken) =>
            Task.FromResult(Stocks.Any(s => s.ProductId == productId && s.BranchId == branchId));

        public Task AddAsync(Stock stock, CancellationToken cancellationToken)
        {
            Stocks.Add(stock);
            return Task.CompletedTask;
        }

        public void Delete(Stock stock) => Stocks.Remove(stock);

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        public List<Product> Products { get; } = [];

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
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> SkuExistsAsync(string sku, Guid? excludeId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> HasStockAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AddAsync(Product product, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
