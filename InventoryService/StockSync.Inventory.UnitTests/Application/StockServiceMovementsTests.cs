using StockSync.Inventory.Application.Common.Exceptions;
using StockSync.Inventory.Application.Movements;
using StockSync.Inventory.Application.Stocks;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Exceptions;
using StockSync.Inventory.Domain.Repositories;

namespace StockSync.Inventory.UnitTests.Application;

// Reuse StockServiceTests in-memory repositories and product creation helpers.
public partial class StockServiceTests
{
    private readonly FakeMovementRepository _movements = new();
    private StockMovementService CreateMovementService() => new(_stockRepository, _productRepository, _movements, _unitOfWork);

    [Fact]
    public async Task Movements_ReceiptAndIssue_SaveBalanceAndHistory()
    {
        var product = CreateProduct();
        var stock = await _service.CreateAsync(new StockRequest(product.Id, Guid.NewGuid(), 0), default);
        var service = CreateMovementService();

        var entry = await service.RecordReceiptAsync(stock.Id, new(5), default);
        var issue = await service.RecordIssueAsync(stock.Id, new(5), default);

        Assert.Equal("Receipt", entry.Type);
        Assert.Equal("Issue", issue.Type);
        Assert.Equal(0, (await _service.GetByIdAsync(stock.Id, default)).Quantity);
        Assert.Equal(2, _movements.Items.Count);
        Assert.Equal(3, _stockRepository.SaveCount);
        Assert.Equal(issue, await service.GetByIdAsync(stock.Id, issue.Id, default));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(6)]
    public async Task Movements_RejectedIssue_DoesNotSaveOrRecordHistory(int quantity)
    {
        var product = CreateProduct();
        var stock = await _service.CreateAsync(new StockRequest(product.Id, Guid.NewGuid(), 5), default);
        var service = CreateMovementService();

        if (quantity <= 0)
            await Assert.ThrowsAsync<ValidationException>(() => service.RecordIssueAsync(stock.Id, new(quantity), default));
        else
            await Assert.ThrowsAsync<InsufficientStockException>(() => service.RecordIssueAsync(stock.Id, new(quantity), default));

        Assert.Equal(5, (await _service.GetByIdAsync(stock.Id, default)).Quantity);
        Assert.Equal(StockMovementType.Adjustment, Assert.Single(_movements.Items).Type);
        Assert.Equal(1, _stockRepository.SaveCount);
    }

    [Fact]
    public async Task Movements_MissingStock_ReturnsNotFound()
    {
        var service = CreateMovementService();
        await Assert.ThrowsAsync<NotFoundException>(() => service.RecordReceiptAsync(Guid.NewGuid(), new(1), default));
        Assert.Empty(_movements.Items);
        Assert.Equal(0, _stockRepository.SaveCount);
    }

    [Fact]
    public async Task Movements_InactiveProduct_DoesNotModifyStock()
    {
        var product = CreateProduct();
        var stock = await _service.CreateAsync(new StockRequest(product.Id, Guid.NewGuid(), 5), default);
        product.Deactivate();

        await Assert.ThrowsAsync<ConflictException>(() => CreateMovementService().RecordIssueAsync(stock.Id, new(1), default));
        Assert.Equal(5, (await _service.GetByIdAsync(stock.Id, default)).Quantity);
        Assert.Equal(StockMovementType.Adjustment, Assert.Single(_movements.Items).Type);
    }

    [Fact]
    public async Task Movements_History_RespectsStockAndPagination()
    {
        var product = CreateProduct();
        var stock = await _service.CreateAsync(new StockRequest(product.Id, Guid.NewGuid(), 0), default);
        var otherProduct = await _service.CreateAsync(new StockRequest(product.Id, Guid.NewGuid(), 0), default);
        var service = CreateMovementService();
        var first = await service.RecordReceiptAsync(stock.Id, new(5), default);
        await service.RecordIssueAsync(stock.Id, new(2), default);
        await service.RecordReceiptAsync(otherProduct.Id, new(9), default);

        var page = await service.ListAsync(stock.Id, new(2, 1), default);

        Assert.Equal(2, page.Total);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal(first, Assert.Single(page.Items));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(otherProduct.Id, first.Id, default));
        await Assert.ThrowsAsync<ValidationException>(() => service.ListAsync(stock.Id, new(0, 20), default));
        await Assert.ThrowsAsync<NotFoundException>(() => service.ListAsync(Guid.NewGuid(), new(), default));
    }

    private sealed class FakeMovementRepository : IStockMovementRepository
    {
        public Task<(IReadOnlyList<StockMovementDetail> Items, int Total)> ListAllAsync(
            int skip, int take, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Product listings are tested against PostgreSQL in integration tests.");

        public List<StockMovement> Items { get; } = [];

        public Task AddAsync(StockMovement movement, CancellationToken cancellationToken)
        {
            Items.Add(movement);
            return Task.CompletedTask;
        }

        public Task<StockMovement?> GetByIdAsync(Guid stockId, Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Items.FirstOrDefault(m => m.StockId == stockId && m.Id == id));

        public Task<(IReadOnlyList<StockMovement> Items, int Total)> ListAsync(Guid stockId, int skip, int take, CancellationToken cancellationToken)
        {
            var filtered = Items.Where(m => m.StockId == stockId).OrderByDescending(m => m.Date).ThenByDescending(m => m.Id).ToList();
            return Task.FromResult<(IReadOnlyList<StockMovement>, int)>((filtered.Skip(skip).Take(take).ToList(), filtered.Count));
        }
    }
}
