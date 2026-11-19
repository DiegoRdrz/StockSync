using StockSync.Orders.Application.Common.Exceptions;
using StockSync.Orders.Application.IntegrationEvents;
using StockSync.Orders.Application.Inventory;
using StockSync.Orders.Application.Lines;
using StockSync.Orders.Application.Orders;
using StockSync.Orders.Domain.Entities;
using StockSync.Orders.Domain.Exceptions;

namespace StockSync.Orders.UnitTests.Application;

public class OrderServiceTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _branchId = Guid.NewGuid();
    private readonly FakeOrderRepository _orders = new();
    private readonly FakeInventoryClient _inventory = new();
    private readonly FakeOutbox _outbox = new();
    private readonly InMemoryUnitOfWork _unitOfWork = new();
    private readonly OrderService _service;

    public OrderServiceTests()
    {
        _service = new OrderService(_orders, _inventory, _outbox, new FixedTenant(_tenantId), _unitOfWork);
    }

    private async Task<OrderResponse> CreateOrderAsync() =>
        await _service.CreateAsync(new OrderRequest(_branchId, null), default);

    [Fact]
    public async Task CreateAsync_SavesDraftAndEnqueuesOrderCreated()
    {
        var response = await _service.CreateAsync(new OrderRequest(_branchId, "Counter"), default);

        Assert.Equal("Draft", response.Status);
        Assert.Equal(1, _orders.SaveCount);
        var created = Assert.IsType<OrderCreated>(Assert.Single(_outbox.Events));
        Assert.Equal(response.Id, created.OrderId);
        Assert.Equal(_tenantId, created.TenantId);
        Assert.Equal(OrderCreated.Name, created.EventType);
    }

    [Fact]
    public async Task CreateAsync_InvalidRequest_ThrowsValidationWithoutSaving()
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateAsync(new OrderRequest(Guid.Empty, new string('x', 501)), default));

        Assert.Contains(nameof(OrderRequest.BranchId), exception.Errors.Keys);
        Assert.Contains(nameof(OrderRequest.Notes), exception.Errors.Keys);
        Assert.Empty(_orders.Orders);
        Assert.Empty(_outbox.Events);
    }

    [Fact]
    public async Task AddLineAsync_TakesSalePriceAndStockFromInventory()
    {
        var order = await CreateOrderAsync();
        var (product, stockId) = _inventory.AddProduct(_branchId, balance: 10, salePrice: 12.50m, purchasePrice: 8m);

        var response = await _service.AddLineAsync(order.Id, new LineRequest(product.Id, 2), default);

        var line = Assert.Single(response.Lines);
        Assert.Equal(12.50m, line.UnitPrice);
        Assert.Equal(25m, line.LineTotal);
        Assert.Equal(stockId, line.StockId);
        Assert.Equal(product.Sku, line.ProductSku);
        Assert.Equal(25m, response.Total);
    }

    [Fact]
    public async Task AddLineAsync_UnknownProduct_ThrowsValidation()
    {
        var order = await CreateOrderAsync();

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.AddLineAsync(order.Id, new LineRequest(Guid.NewGuid(), 1), default));

        Assert.Contains(nameof(LineRequest.ProductId), exception.Errors.Keys);
    }

    [Fact]
    public async Task AddLineAsync_ProductNotAssignedToBranch_ThrowsValidation()
    {
        var order = await CreateOrderAsync();
        var (product, _) = _inventory.AddProduct(Guid.NewGuid(), balance: 10);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.AddLineAsync(order.Id, new LineRequest(product.Id, 1), default));

        Assert.Contains("not assigned to branch", exception.Errors[nameof(LineRequest.ProductId)][0]);
    }

    [Fact]
    public async Task AddLineAsync_ProductWithoutSalePrice_ThrowsValidation()
    {
        var order = await CreateOrderAsync();
        var (product, _) = _inventory.AddProduct(_branchId, balance: 10, salePrice: 0m);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.AddLineAsync(order.Id, new LineRequest(product.Id, 1), default));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task AddLineAsync_InvalidQuantity_ThrowsValidation(int quantity)
    {
        var order = await CreateOrderAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.AddLineAsync(order.Id, new LineRequest(Guid.NewGuid(), quantity), default));
    }

    [Fact]
    public async Task AddLineAsync_MissingOrder_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.AddLineAsync(Guid.NewGuid(), new LineRequest(Guid.NewGuid(), 1), default));
    }

    [Fact]
    public async Task UpdateLineAsync_UnknownLine_ThrowsNotFound()
    {
        var order = await CreateOrderAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateLineAsync(order.Id, Guid.NewGuid(), new LineQuantityRequest(2), default));
    }

    [Fact]
    public async Task ConfirmAsync_EnoughStock_IssuesEveryLineAndPublishesSaleConfirmed()
    {
        var order = await CreateOrderAsync();
        var (hammer, hammerStock) = _inventory.AddProduct(_branchId, balance: 5, salePrice: 10m);
        var (drill, drillStock) = _inventory.AddProduct(_branchId, balance: 3, salePrice: 50m);
        await _service.AddLineAsync(order.Id, new LineRequest(hammer.Id, 2), default);
        await _service.AddLineAsync(order.Id, new LineRequest(drill.Id, 3), default);

        var response = await _service.ConfirmAsync(order.Id, default);

        Assert.Equal("Confirmed", response.Status);
        Assert.NotNull(response.ConfirmedAt);
        Assert.All(response.Lines, l => Assert.Equal("Applied", l.StockStatus));
        Assert.All(response.Lines, l => Assert.NotNull(l.StockMovementId));
        Assert.Equal(3, _inventory.Balances[hammerStock]);
        Assert.Equal(0, _inventory.Balances[drillStock]);

        var confirmed = Assert.IsType<SaleConfirmed>(_outbox.Events.Last());
        Assert.Equal(170m, confirmed.Total);
        Assert.Equal(2, confirmed.Lines.Count);
    }

    [Fact]
    public async Task ConfirmAsync_InsufficientStock_CompensatesIssuedLinesAndReturnsToDraft()
    {
        var order = await CreateOrderAsync();
        var (hammer, hammerStock) = _inventory.AddProduct(_branchId, balance: 5, sku: "A-HAMMER");
        var (drill, drillStock) = _inventory.AddProduct(_branchId, balance: 1, sku: "B-DRILL");
        await _service.AddLineAsync(order.Id, new LineRequest(hammer.Id, 2), default);
        await _service.AddLineAsync(order.Id, new LineRequest(drill.Id, 3), default);

        var exception = await Assert.ThrowsAsync<ConflictException>(() => _service.ConfirmAsync(order.Id, default));

        Assert.Contains("B-DRILL", exception.Message);
        Assert.Contains("Insufficient stock", exception.Message);
        // The hammer was issued and then compensated with a receipt: balances are unchanged.
        Assert.Equal(5, _inventory.Balances[hammerStock]);
        Assert.Equal(1, _inventory.Balances[drillStock]);
        Assert.Equal(new[] { "Issue", "Receipt" }, _inventory.Movements.Select(m => m.Kind));

        var stored = await _service.GetByIdAsync(order.Id, default);
        Assert.Equal("Draft", stored.Status);
        Assert.All(stored.Lines, l => Assert.Equal("Pending", l.StockStatus));
        Assert.DoesNotContain(_outbox.Events, e => e is SaleConfirmed);
    }

    [Fact]
    public async Task ConfirmAsync_IssuesInLineOrderAndCompensatesInReverseOrder()
    {
        var order = await CreateOrderAsync();
        var (first, firstStock) = _inventory.AddProduct(_branchId, balance: 5, sku: "Z-FIRST");
        var (second, secondStock) = _inventory.AddProduct(_branchId, balance: 5, sku: "Y-SECOND");
        var (third, thirdStock) = _inventory.AddProduct(_branchId, balance: 0, sku: "X-THIRD");
        await _service.AddLineAsync(order.Id, new LineRequest(first.Id, 1), default);
        await _service.AddLineAsync(order.Id, new LineRequest(second.Id, 1), default);
        await _service.AddLineAsync(order.Id, new LineRequest(third.Id, 1), default);

        await Assert.ThrowsAsync<ConflictException>(() => _service.ConfirmAsync(order.Id, default));

        Assert.Equal(
            new[] { ("Issue", firstStock), ("Issue", secondStock), ("Receipt", secondStock), ("Receipt", firstStock) },
            _inventory.Movements.Select(m => (m.Kind, m.StockId)));
        Assert.Equal(new[] { 1, 2, 3 }, (await _service.GetByIdAsync(order.Id, default)).Lines.Select(l => l.LineNumber));
    }

    [Fact]
    public async Task ConfirmAsync_AfterRejection_CanBeConfirmedOnceFixed()
    {
        var order = await CreateOrderAsync();
        var (drill, _) = _inventory.AddProduct(_branchId, balance: 1);
        var added = await _service.AddLineAsync(order.Id, new LineRequest(drill.Id, 3), default);
        await Assert.ThrowsAsync<ConflictException>(() => _service.ConfirmAsync(order.Id, default));

        await _service.UpdateLineAsync(order.Id, added.Lines[0].Id, new LineQuantityRequest(1), default);
        var response = await _service.ConfirmAsync(order.Id, default);

        Assert.Equal("Confirmed", response.Status);
    }

    [Fact]
    public async Task ConfirmAsync_UnknownResult_LeavesOrderForReviewWithoutRetryOrCompensation()
    {
        var order = await CreateOrderAsync();
        var (hammer, _) = _inventory.AddProduct(_branchId, balance: 5, sku: "A-HAMMER");
        var (drill, drillStock) = _inventory.AddProduct(_branchId, balance: 5, sku: "B-DRILL");
        await _service.AddLineAsync(order.Id, new LineRequest(hammer.Id, 1), default);
        await _service.AddLineAsync(order.Id, new LineRequest(drill.Id, 1), default);
        _inventory.Override = (kind, stockId) => stockId == drillStock
            ? StockMovementResult.Unknown("Inventory did not respond in time.")
            : null;

        var exception = await Assert.ThrowsAsync<InventoryUnavailableException>(() =>
            _service.ConfirmAsync(order.Id, default));

        Assert.Contains("requires review", exception.Message);
        var stored = await _service.GetByIdAsync(order.Id, default);
        Assert.Equal("RequiresReview", stored.Status);
        Assert.Contains("B-DRILL", stored.ReviewReason);
        // The hammer issue is kept and recorded so a reviewer can reconcile it.
        Assert.Equal("Applied", stored.Lines.Single(l => l.ProductSku == "A-HAMMER").StockStatus);
        Assert.Single(_inventory.Movements);
        Assert.DoesNotContain(_outbox.Events, e => e is SaleConfirmed);
    }

    [Fact]
    public async Task ConfirmAsync_CompensationFails_LeavesOrderForReview()
    {
        var order = await CreateOrderAsync();
        var (hammer, _) = _inventory.AddProduct(_branchId, balance: 5, sku: "A-HAMMER");
        var (drill, _) = _inventory.AddProduct(_branchId, balance: 0, sku: "B-DRILL");
        await _service.AddLineAsync(order.Id, new LineRequest(hammer.Id, 1), default);
        await _service.AddLineAsync(order.Id, new LineRequest(drill.Id, 1), default);
        _inventory.Override = (kind, _) => kind == "Receipt"
            ? StockMovementResult.NotApplied("Inventory is unavailable.")
            : null;

        await Assert.ThrowsAsync<InventoryUnavailableException>(() => _service.ConfirmAsync(order.Id, default));

        var stored = await _service.GetByIdAsync(order.Id, default);
        Assert.Equal("RequiresReview", stored.Status);
        Assert.Contains("Compensation", stored.ReviewReason);
    }

    [Fact]
    public async Task ConfirmAsync_InventoryDownBeforeFirstIssue_ReturnsToDraft()
    {
        var order = await CreateOrderAsync();
        var (hammer, hammerStock) = _inventory.AddProduct(_branchId, balance: 5);
        await _service.AddLineAsync(order.Id, new LineRequest(hammer.Id, 1), default);
        _inventory.Override = (_, _) => StockMovementResult.NotApplied("Inventory is unavailable.");

        await Assert.ThrowsAsync<InventoryUnavailableException>(() => _service.ConfirmAsync(order.Id, default));

        Assert.Equal("Draft", (await _service.GetByIdAsync(order.Id, default)).Status);
        Assert.Equal(5, _inventory.Balances[hammerStock]);
    }

    [Fact]
    public async Task ConfirmAsync_EmptyOrder_ThrowsDomainException()
    {
        var order = await CreateOrderAsync();

        await Assert.ThrowsAsync<DomainException>(() => _service.ConfirmAsync(order.Id, default));
        Assert.Empty(_inventory.Movements);
    }

    [Fact]
    public async Task ConfirmAsync_Twice_ThrowsConflictAndDoesNotIssueAgain()
    {
        var order = await CreateOrderAsync();
        var (hammer, hammerStock) = _inventory.AddProduct(_branchId, balance: 5);
        await _service.AddLineAsync(order.Id, new LineRequest(hammer.Id, 1), default);
        await _service.ConfirmAsync(order.Id, default);

        await Assert.ThrowsAsync<DomainConflictException>(() => _service.ConfirmAsync(order.Id, default));

        Assert.Equal(4, _inventory.Balances[hammerStock]);
        Assert.Single(_inventory.Movements);
    }

    [Fact]
    public async Task CancelAsync_Draft_CancelsWithoutTouchingInventory()
    {
        var order = await CreateOrderAsync();

        var response = await _service.CancelAsync(order.Id, default);

        Assert.Equal("Cancelled", response.Status);
        Assert.Empty(_inventory.Movements);
    }

    [Fact]
    public async Task ListAsync_FiltersByStatusAndBranch()
    {
        var draft = await CreateOrderAsync();
        var cancelled = await CreateOrderAsync();
        await _service.CancelAsync(cancelled.Id, default);
        await _service.CreateAsync(new OrderRequest(Guid.NewGuid(), null), default);

        var result = await _service.ListAsync(new OrderFilter { Status = OrderStatus.Draft, BranchId = _branchId }, default);

        Assert.Equal(draft.Id, Assert.Single(result.Items).Id);
        Assert.Equal(1, result.Total);
    }

    [Fact]
    public async Task ListAsync_InvalidFilter_ThrowsValidation()
    {
        var filter = new OrderFilter
        {
            Page = 0,
            PageSize = 101,
            From = new DateTime(2026, 2, 1),
            To = new DateTime(2026, 1, 1)
        };

        var exception = await Assert.ThrowsAsync<ValidationException>(() => _service.ListAsync(filter, default));

        Assert.Contains(nameof(OrderFilter.Page), exception.Errors.Keys);
        Assert.Contains(nameof(OrderFilter.PageSize), exception.Errors.Keys);
        Assert.Contains(nameof(OrderFilter.From), exception.Errors.Keys);
    }
}
