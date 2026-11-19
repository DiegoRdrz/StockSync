using StockSync.Orders.Application.Common.Exceptions;
using StockSync.Orders.Application.Inventory;
using StockSync.Orders.Application.Lines;
using StockSync.Orders.Application.PurchaseOrders;
using StockSync.Orders.Domain.Exceptions;

namespace StockSync.Orders.UnitTests.Application;

public class PurchaseOrderServiceTests
{
    private readonly Guid _branchId = Guid.NewGuid();
    private readonly FakePurchaseOrderRepository _purchaseOrders = new();
    private readonly FakeInventoryClient _inventory = new();
    private readonly PurchaseOrderService _service;

    public PurchaseOrderServiceTests()
    {
        _service = new PurchaseOrderService(_purchaseOrders, _inventory, new InMemoryUnitOfWork());
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
    public async Task ReceiveAsync_Placed_RecordsReceiptsAndCompletes()
    {
        var purchaseOrder = await CreateAsync();
        var (product, stockId) = _inventory.AddProduct(_branchId, balance: 2);
        await _service.AddLineAsync(purchaseOrder.Id, new LineRequest(product.Id, 10), default);
        await _service.PlaceAsync(purchaseOrder.Id, default);

        var response = await _service.ReceiveAsync(purchaseOrder.Id, default);

        Assert.Equal("Received", response.Status);
        Assert.Equal(12, _inventory.Balances[stockId]);
        Assert.Equal("Receipt", Assert.Single(_inventory.Movements).Kind);
    }

    [Fact]
    public async Task ReceiveAsync_NotPlaced_ThrowsConflictWithoutMovements()
    {
        var purchaseOrder = await CreateAsync();
        var (product, _) = _inventory.AddProduct(_branchId, balance: 2);
        await _service.AddLineAsync(purchaseOrder.Id, new LineRequest(product.Id, 10), default);

        await Assert.ThrowsAsync<DomainConflictException>(() => _service.ReceiveAsync(purchaseOrder.Id, default));
        Assert.Empty(_inventory.Movements);
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
