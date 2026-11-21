using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockSync.Orders.Application.Common.Exceptions;
using StockSync.Orders.Application.IntegrationEvents;
using StockSync.Orders.Application.Inventory;
using StockSync.Orders.Application.Lines;
using StockSync.Orders.Application.Orders;
using StockSync.Orders.Application.PurchaseOrders;
using StockSync.Orders.Domain.Entities;
using StockSync.Orders.Domain.Exceptions;
using StockSync.Orders.Infrastructure;
using StockSync.Orders.Infrastructure.Outbox;
using StockSync.Orders.Infrastructure.Repositories;

namespace StockSync.Orders.IntegrationTests;

public class OrdersPostgresTests : PostgresTestBase
{
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _branchId = Guid.NewGuid();
    private readonly StubInventory _inventory = new();

    private OrderService CreateOrderService(OrdersDbContext context, Guid tenantId) =>
        new(new OrderRepository(context), _inventory, new EfIntegrationEventOutbox(context),
            new FixedTenant(tenantId), new UnitOfWork(context));

    private PurchaseOrderService CreatePurchaseOrderService(OrdersDbContext context) =>
        new(new PurchaseOrderRepository(context), _inventory, new EfIntegrationEventOutbox(context),
            new FixedTenant(context.TenantId), new UnitOfWork(context));

    [PostgreSqlFact]
    public async Task Order_LinesTotalsAndConfirmation_ArePersisted()
    {
        var hammer = _inventory.Add(_branchId, balance: 5, salePrice: 12.35m);
        var drill = _inventory.Add(_branchId, balance: 5, salePrice: 80m);
        Guid orderId;
        await using (var context = CreateContext(_tenantA))
        {
            var service = CreateOrderService(context, _tenantA);
            orderId = (await service.CreateAsync(new OrderRequest(_branchId, "Counter"), default)).Id;
            await service.AddLineAsync(orderId, new LineRequest(hammer, 2), default);
            var withDrill = await service.AddLineAsync(orderId, new LineRequest(drill, 1), default);
            // Removing a line deletes its row (orphan removal).
            await service.RemoveLineAsync(orderId, withDrill.Lines.Single(l => l.ProductId == drill).Id, default);
            await service.ConfirmAsync(orderId, default);
        }

        await using (var context = CreateContext(_tenantA))
        {
            var order = await context.Orders.Include(o => o.Lines).SingleAsync(o => o.Id == orderId);
            Assert.Equal(OrderStatus.Confirmed, order.Status);
            Assert.Equal(24.70m, order.Total);
            var line = Assert.Single(order.Lines);
            Assert.Equal(StockLineStatus.Applied, line.StockStatus);
            Assert.NotNull(line.StockMovementId);
            Assert.Equal(1, await context.OrderLines.CountAsync());

            var events = await context.OutboxMessages.OrderBy(m => m.OccurredAt).ToListAsync();
            Assert.Equal(["orders.order-created.v1", "orders.sale-confirmed.v1"], events.Select(e => e.EventType));
            Assert.All(events, e => Assert.Equal(_tenantA, e.TenantId));
            using var payload = JsonDocument.Parse(events[1].Payload);
            Assert.Equal(orderId, payload.RootElement.GetProperty("orderId").GetGuid());
            Assert.Equal(24.70m, payload.RootElement.GetProperty("total").GetDecimal());
            Assert.Equal(2, payload.RootElement.GetProperty("lines")[0].GetProperty("quantity").GetInt32());
        }
    }

    [PostgreSqlFact]
    public async Task Order_RejectedConfirmation_IsStoredAsDraftWithoutSaleEvent()
    {
        var hammer = _inventory.Add(_branchId, balance: 1, salePrice: 10m);
        await using var context = CreateContext(_tenantA);
        var service = CreateOrderService(context, _tenantA);
        var orderId = (await service.CreateAsync(new OrderRequest(_branchId, null), default)).Id;
        await service.AddLineAsync(orderId, new LineRequest(hammer, 3), default);

        await Assert.ThrowsAsync<ConflictException>(() => service.ConfirmAsync(orderId, default));

        await using var check = CreateContext(_tenantA);
        Assert.Equal(OrderStatus.Draft, (await check.Orders.SingleAsync()).Status);
        Assert.DoesNotContain(await check.OutboxMessages.ToListAsync(), m => m.EventType == "orders.sale-confirmed.v1");
    }

    [PostgreSqlFact]
    public async Task ConcurrentConfirmations_IssueStockOnlyOnce()
    {
        var hammer = _inventory.Add(_branchId, balance: 5, salePrice: 10m);
        Guid orderId;
        await using (var context = CreateContext(_tenantA))
        {
            var service = CreateOrderService(context, _tenantA);
            orderId = (await service.CreateAsync(new OrderRequest(_branchId, null), default)).Id;
            await service.AddLineAsync(orderId, new LineRequest(hammer, 2), default);
        }

        // The row lock serializes Draft -> Confirming; the second request sees Confirming or Confirmed.
        async Task<Exception?> ConfirmAsync()
        {
            await using var context = CreateContext(_tenantA);
            try
            {
                await CreateOrderService(context, _tenantA).ConfirmAsync(orderId, default);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        var results = await Task.WhenAll(ConfirmAsync(), ConfirmAsync());

        Assert.Single(results, r => r is null);
        Assert.Single(results, r => r is DomainConflictException);
        Assert.Equal(3, _inventory.Balance(hammer));
    }

    [PostgreSqlFact]
    public async Task TenantData_IsInvisibleToAnotherTenant()
    {
        var hammer = _inventory.Add(_branchId, balance: 5, salePrice: 10m);
        Guid orderId;
        await using (var contextA = CreateContext(_tenantA))
        {
            var service = CreateOrderService(contextA, _tenantA);
            orderId = (await service.CreateAsync(new OrderRequest(_branchId, null), default)).Id;
            await service.AddLineAsync(orderId, new LineRequest(hammer, 1), default);
        }

        await using var contextB = CreateContext(_tenantB);
        var serviceB = CreateOrderService(contextB, _tenantB);
        await Assert.ThrowsAsync<NotFoundException>(() => serviceB.GetByIdAsync(orderId, default));
        await Assert.ThrowsAsync<NotFoundException>(() => serviceB.CancelAsync(orderId, default));
        Assert.Equal(0, (await serviceB.ListAsync(new OrderFilter(), default)).Total);
        Assert.Empty(await contextB.OrderLines.ToListAsync());
    }

    [PostgreSqlFact]
    public async Task List_FiltersByStatusBranchAndDate()
    {
        await using var context = CreateContext(_tenantA);
        var service = CreateOrderService(context, _tenantA);
        var kept = await service.CreateAsync(new OrderRequest(_branchId, null), default);
        var cancelled = await service.CreateAsync(new OrderRequest(_branchId, null), default);
        await service.CancelAsync(cancelled.Id, default);
        await service.CreateAsync(new OrderRequest(Guid.NewGuid(), null), default);

        var drafts = await service.ListAsync(
            new OrderFilter { Status = OrderStatus.Draft, BranchId = _branchId }, default);
        var future = await service.ListAsync(
            new OrderFilter { From = DateTime.UtcNow.AddDays(1) }, default);
        // A date without kind is interpreted as UTC instead of failing in Npgsql.
        var unspecified = await service.ListAsync(
            new OrderFilter { From = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-1), DateTimeKind.Unspecified) }, default);

        Assert.Equal(kept.Id, Assert.Single(drafts.Items).Id);
        Assert.Equal(0, future.Total);
        Assert.Equal(3, unspecified.Total);
    }

    [PostgreSqlFact]
    public async Task PurchaseOrder_ReceiveFlow_PersistsReceivedStateAndOutboxEvent()
    {
        var hammer = _inventory.Add(_branchId, balance: 0, purchasePrice: 7.25m);
        await using (var context = CreateContext(_tenantA))
        {
            var service = CreatePurchaseOrderService(context);
            var purchaseOrder = await service.CreateAsync(new PurchaseOrderRequest(_branchId, "Acme", null), default);
            await service.AddLineAsync(purchaseOrder.Id, new LineRequest(hammer, 4), default);
            await service.PlaceAsync(purchaseOrder.Id, default);
            await service.ReceiveAsync(purchaseOrder.Id, default);
        }

        await using var check = CreateContext(_tenantA);
        var stored = await check.PurchaseOrders.Include(p => p.Lines).SingleAsync();
        Assert.Equal(PurchaseOrderStatus.Received, stored.Status);
        Assert.Equal(29m, stored.Total);
        var storedLine = Assert.Single(stored.Lines);
        Assert.Equal(StockLineStatus.Applied, storedLine.StockStatus);
        Assert.Equal(4, _inventory.Balance(hammer));

        var message = Assert.Single(await check.OutboxMessages.ToListAsync());
        Assert.Equal("orders.purchase-order-received.v1", message.EventType);
        Assert.Equal(_tenantA, message.TenantId);
        Assert.Null(message.ProcessedAt);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var received = Assert.IsType<PurchaseOrderReceived>(JsonSerializer.Deserialize<PurchaseOrderReceived>(message.Payload, options));
        Assert.Equal(message.Id, received.EventId);
        var storedPrecision = received.OccurredAt.AddTicks(-(received.OccurredAt.Ticks % TimeSpan.TicksPerMicrosecond));
        Assert.Equal(storedPrecision, message.OccurredAt);
        Assert.Equal(_tenantA, received.TenantId);
        Assert.Equal(stored.Id, received.PurchaseOrderId);
        Assert.Equal(stored.ReceivedAt, received.ReceivedAt);
        Assert.Equal(stored.Total, received.Total);
        Assert.Equal(storedLine.StockMovementId, Assert.Single(received.Lines).StockMovementId);
    }

    [PostgreSqlFact]
    public async Task PurchaseOrder_FinalCommitFailure_RollsBackReceivedStateAndOutboxEvent()
    {
        var hammer = _inventory.Add(_branchId, balance: 0);
        Guid purchaseOrderId;
        await using (var context = CreateContext(_tenantA))
        {
            var service = CreatePurchaseOrderService(context);
            purchaseOrderId = (await service.CreateAsync(new PurchaseOrderRequest(_branchId, "Acme", null), default)).Id;
            await service.AddLineAsync(purchaseOrderId, new LineRequest(hammer, 4), default);
            await service.PlaceAsync(purchaseOrderId, default);

            // A deferred trigger checks that the insert is visible at commit before forcing PostgreSQL to roll it back.
            await context.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION reject_purchase_receipt() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM "OutboxMessages"
                        WHERE "EventType" = 'orders.purchase-order-received.v1'
                          AND "TenantId" = NEW."TenantId"
                          AND "Payload"->>'purchaseOrderId' = NEW."Id"::text
                    ) THEN
                        RAISE EXCEPTION 'Purchase receipt event missing at commit';
                    END IF;
                    RAISE EXCEPTION 'Forced failure at purchase receipt commit';
                END;
                $$;

                CREATE CONSTRAINT TRIGGER reject_purchase_receipt
                AFTER UPDATE ON "PurchaseOrders" DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW WHEN (NEW."Status" = 'Received')
                EXECUTE FUNCTION reject_purchase_receipt();
                """);

            var error = await Record.ExceptionAsync(() => service.ReceiveAsync(purchaseOrderId, default));
            var postgresError = Assert.IsType<PostgresException>(error?.GetBaseException());
            Assert.Equal("Forced failure at purchase receipt commit", postgresError.MessageText);
        }

        await using var check = CreateContext(_tenantA);
        var stored = await check.PurchaseOrders.Include(p => p.Lines).SingleAsync(p => p.Id == purchaseOrderId);
        Assert.Equal(PurchaseOrderStatus.Receiving, stored.Status);
        Assert.Null(stored.ReceivedAt);
        Assert.Equal(StockLineStatus.Applied, Assert.Single(stored.Lines).StockStatus);
        Assert.Equal(4, _inventory.Balance(hammer));
        Assert.Empty(await check.OutboxMessages.ToListAsync());
    }

    // Inventory stub with real balances, enough to drive the saga against PostgreSQL.
    private sealed class StubInventory : IInventoryClient
    {
        private readonly Dictionary<Guid, (InventoryProduct Product, Guid BranchId, Guid StockId)> _products = new();
        private readonly Dictionary<Guid, int> _balances = new();

        public Guid Add(Guid branchId, int balance, decimal salePrice = 10m, decimal purchasePrice = 5m)
        {
            var product = new InventoryProduct(Guid.NewGuid(), "Product", $"SKU-{_products.Count + 1}", purchasePrice, salePrice);
            var stockId = Guid.NewGuid();
            _products[product.Id] = (product, branchId, stockId);
            _balances[stockId] = balance;
            return product.Id;
        }

        public int Balance(Guid productId) => _balances[_products[productId].StockId];

        public Task<InventoryProduct?> GetProductAsync(Guid productId, CancellationToken cancellationToken) =>
            Task.FromResult(_products.TryGetValue(productId, out var p) ? p.Product : null);

        public Task<Guid?> FindStockIdAsync(Guid productId, Guid branchId, CancellationToken cancellationToken) =>
            Task.FromResult(_products.TryGetValue(productId, out var p) && p.BranchId == branchId ? p.StockId : (Guid?)null);

        public Task<StockMovementResult> IssueAsync(Guid stockId, int quantity, CancellationToken cancellationToken)
        {
            if (_balances[stockId] < quantity)
                return Task.FromResult(StockMovementResult.Rejected("Insufficient stock."));
            _balances[stockId] -= quantity;
            return Task.FromResult(StockMovementResult.Applied(Guid.NewGuid()));
        }

        public Task<StockMovementResult> ReceiveAsync(Guid stockId, int quantity, CancellationToken cancellationToken)
        {
            _balances[stockId] += quantity;
            return Task.FromResult(StockMovementResult.Applied(Guid.NewGuid()));
        }
    }
}
