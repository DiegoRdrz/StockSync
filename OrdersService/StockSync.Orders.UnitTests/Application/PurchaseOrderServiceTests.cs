using StockSync.Orders.Application.Common.Exceptions;
using StockSync.Orders.Application.IntegrationEvents;
using StockSync.Orders.Application.Inventory;
using StockSync.Orders.Application.Lines;
using StockSync.Orders.Application.PurchaseOrders;
using StockSync.Orders.Domain.Exceptions;

namespace StockSync.Orders.UnitTests.Application;

public class PurchaseOrderServiceTests
{
    private readonly Guid _branchId = Guid.NewGuid();
    private readonly FixedTenant _tenant = new(Guid.NewGuid());
    private readonly FakeOutbox _outbox = new();
    private readonly FakePurchaseOrderRepository _purchaseOrders = new();
    private readonly FakeInventoryClient _inventory = new();
    private readonly PurchaseOrderService _service;

    public PurchaseOrderServiceTests()
    {
        _service = new PurchaseOrderService(_purchaseOrders, _inventory, _outbox, _tenant, new InMemoryUnitOfWork());
    }

    private Task<PurchaseOrderResponse> CreateAsync() =>
        _service.CreateAsync(new PurchaseOrderRequest(_branchId, "Acme Tools", null), default);

    [Fact]
    public async Task CreateAsync_WithoutSupplier_ThrowsValidation()
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateAsync(new PurchaseOrderRequest(_branchId, " ", null), default));

        Assert.Contains(nameof(PurchaseOrderRequest.SupplierName), exception.Errors.Keys);
    }

    [Fact]
    public async Task AddLineAsync_TakesPurchasePriceFromInventory()
    {
        var purchaseOrder = await CreateAsync();
        var (product, _) = _inventory.AddProduct(_branchId, balance: 0, salePrice: 15m, purchasePrice: 9.99m);

        var response = await _service.AddLineAsync(purchaseOrder.Id, new LineRequest(product.Id, 10), default);

        Assert.Equal(9.99m, Assert.Single(response.Lines).UnitPrice);
        Assert.Equal(99.90m, response.Total);
    }

    [Fact]
    public async Task ReceiveAsync_Placed_RecordsReceiptsAndEnqueuesOneEvent()
    {
        var purchaseOrder = await CreateAsync();
        var (hammer, hammerStock) = _inventory.AddProduct(_branchId, balance: 2, purchasePrice: 8m);
        var (drill, drillStock) = _inventory.AddProduct(_branchId, balance: 1, purchasePrice: 29.95m);
        await _service.AddLineAsync(purchaseOrder.Id, new LineRequest(hammer.Id, 10), default);
        await _service.AddLineAsync(purchaseOrder.Id, new LineRequest(drill.Id, 2), default);
        await _service.PlaceAsync(purchaseOrder.Id, default);
        Assert.Empty(_outbox.Events);

        var response = await _service.ReceiveAsync(purchaseOrder.Id, default);

        Assert.Equal("Received", response.Status);
        Assert.Equal(12, _inventory.Balances[hammerStock]);
        Assert.Equal(3, _inventory.Balances[drillStock]);
        Assert.Equal(new[] { "Receipt", "Receipt" }, _inventory.Movements.Select(m => m.Kind));

        var received = Assert.IsType<PurchaseOrderReceived>(Assert.Single(_outbox.Events));
        Assert.NotEqual(Guid.Empty, received.EventId);
        Assert.Equal(DateTimeKind.Utc, received.OccurredAt.Kind);
        Assert.Equal(_tenant.TenantId, received.TenantId);
        Assert.Equal(response.Id, received.PurchaseOrderId);
        Assert.Equal(response.BranchId, received.BranchId);
        Assert.Equal(response.SupplierName, received.SupplierName);
        Assert.Equal(response.ReceivedAt, received.ReceivedAt);
        Assert.Equal(response.Total, received.Total);
        Assert.Equal(response.Lines.Select(l => new PurchaseOrderReceivedLine(
            l.LineNumber, l.ProductId, l.ProductSku, l.ProductName, l.Quantity, l.UnitPrice, l.LineTotal, l.StockMovementId)),
            received.Lines);
        Assert.Equal(_inventory.Movements.Select(m => (Guid?)m.Id), received.Lines.Select(l => l.StockMovementId));

        await Assert.ThrowsAsync<DomainConflictException>(() => _service.ReceiveAsync(purchaseOrder.Id, default));
        Assert.Single(_outbox.Events);
    }

    [Fact]
    public async Task ReceiveAsync_NotPlaced_ThrowsConflictWithoutMovements()
    {
        var purchaseOrder = await CreateAsync();
        var (product, _) = _inventory.AddProduct(_branchId, balance: 2);
        await _service.AddLineAsync(purchaseOrder.Id, new LineRequest(product.Id, 10), default);

        await Assert.ThrowsAsync<DomainConflictException>(() => _service.ReceiveAsync(purchaseOrder.Id, default));
        Assert.Empty(_inventory.Movements);
        Assert.Empty(_outbox.Events);
    }

    [Fact]
    public async Task ReceiveAsync_RejectedLine_CompensatesWithIssueAndReturnsToOrdered()
    {
        var purchaseOrder = await CreateAsync();
        var (hammer, hammerStock) = _inventory.AddProduct(_branchId, balance: 0, sku: "A-HAMMER");
        var (drill, drillStock) = _inventory.AddProduct(_branchId, balance: 0, sku: "B-DRILL");
        await _service.AddLineAsync(purchaseOrder.Id, new LineRequest(hammer.Id, 4), default);
        await _service.AddLineAsync(purchaseOrder.Id, new LineRequest(drill.Id, 4), default);
        await _service.PlaceAsync(purchaseOrder.Id, default);
        _inventory.Override = (kind, stockId) => kind == "Receipt" && stockId == drillStock
            ? StockMovementResult.Rejected("Product is inactive; its stock cannot record movements.")
            : null;

        await Assert.ThrowsAsync<ConflictException>(() => _service.ReceiveAsync(purchaseOrder.Id, default));

        Assert.Equal(0, _inventory.Balances[hammerStock]);
        Assert.Equal(new[] { "Receipt", "Issue" }, _inventory.Movements.Select(m => m.Kind));
        Assert.Equal("Ordered", (await _service.GetByIdAsync(purchaseOrder.Id, default)).Status);
        Assert.Empty(_outbox.Events);
    }

    [Fact]
    public async Task ReceiveAsync_UnknownReceipt_RequiresReviewWithoutEnqueuingEvent()
    {
        var purchaseOrder = await CreateAsync();
        var (product, _) = _inventory.AddProduct(_branchId, balance: 0);
        await _service.AddLineAsync(purchaseOrder.Id, new LineRequest(product.Id, 1), default);
        await _service.PlaceAsync(purchaseOrder.Id, default);
        _inventory.Override = (_, _) => StockMovementResult.Unknown("Receipt timed out.");

        await Assert.ThrowsAsync<InventoryUnavailableException>(() => _service.ReceiveAsync(purchaseOrder.Id, default));

        Assert.Equal("RequiresReview", (await _service.GetByIdAsync(purchaseOrder.Id, default)).Status);
        Assert.Empty(_outbox.Events);
    }

    [Fact]
    public async Task CancelAsync_Ordered_Cancels()
    {
        var purchaseOrder = await CreateAsync();
        var (product, _) = _inventory.AddProduct(_branchId, balance: 0);
        await _service.AddLineAsync(purchaseOrder.Id, new LineRequest(product.Id, 1), default);
        await _service.PlaceAsync(purchaseOrder.Id, default);

        var response = await _service.CancelAsync(purchaseOrder.Id, default);

        Assert.Equal("Cancelled", response.Status);
    }
}
