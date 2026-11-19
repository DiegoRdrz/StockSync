using StockSync.Orders.Application.Common;
using StockSync.Orders.Application.IntegrationEvents;
using StockSync.Orders.Application.Inventory;
using StockSync.Orders.Domain.Entities;
using StockSync.Orders.Domain.Repositories;

namespace StockSync.Orders.UnitTests.Application;

internal sealed class InMemoryUnitOfWork : IUnitOfWork
{
    public int Transactions { get; private set; }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        Transactions++;
        return await operation();
    }

    public Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken)
    {
        Transactions++;
        return operation();
    }
}

internal sealed record FixedTenant(Guid TenantId) : ICurrentTenant;

internal sealed class FakeOutbox : IIntegrationEventOutbox
{
    public List<IIntegrationEvent> Events { get; } = [];

    public void Enqueue(IIntegrationEvent integrationEvent) => Events.Add(integrationEvent);
}

internal sealed class FakeOrderRepository : IOrderRepository
{
    public List<Order> Orders { get; } = [];
    public int SaveCount { get; private set; }

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Orders.FirstOrDefault(o => o.Id == id));

    public Task<Order?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        GetByIdAsync(id, cancellationToken);

    public Task<(IReadOnlyList<Order> Items, int Total)> ListAsync(
        OrderStatus? status, Guid? branchId, DateTime? from, DateTime? to, int skip, int take,
        CancellationToken cancellationToken)
    {
        var query = Orders
            .Where(o => status is null || o.Status == status)
            .Where(o => branchId is null || o.BranchId == branchId)
            .Where(o => from is null || o.CreatedAt >= from)
            .Where(o => to is null || o.CreatedAt <= to)
            .OrderByDescending(o => o.CreatedAt)
            .ToList();
        return Task.FromResult<(IReadOnlyList<Order>, int)>((query.Skip(skip).Take(take).ToList(), query.Count));
    }

    public Task AddAsync(Order order, CancellationToken cancellationToken)
    {
        Orders.Add(order);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

internal sealed class FakePurchaseOrderRepository : IPurchaseOrderRepository
{
    public List<PurchaseOrder> PurchaseOrders { get; } = [];
    public int SaveCount { get; private set; }

    public Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(PurchaseOrders.FirstOrDefault(p => p.Id == id));

    public Task<PurchaseOrder?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        GetByIdAsync(id, cancellationToken);

    public Task<(IReadOnlyList<PurchaseOrder> Items, int Total)> ListAsync(
        PurchaseOrderStatus? status, Guid? branchId, DateTime? from, DateTime? to, int skip, int take,
        CancellationToken cancellationToken)
    {
        var query = PurchaseOrders
            .Where(p => status is null || p.Status == status)
            .Where(p => branchId is null || p.BranchId == branchId)
            .ToList();
        return Task.FromResult<(IReadOnlyList<PurchaseOrder>, int)>((query.Skip(skip).Take(take).ToList(), query.Count));
    }

    public Task AddAsync(PurchaseOrder purchaseOrder, CancellationToken cancellationToken)
    {
        PurchaseOrders.Add(purchaseOrder);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

// Minimal Inventory: products, branch assignments and balances. Failures can be injected per call.
internal sealed class FakeInventoryClient : IInventoryClient
{
    private readonly Dictionary<Guid, InventoryProduct> _products = new();
    private readonly Dictionary<(Guid ProductId, Guid BranchId), Guid> _assignments = new();

    public Dictionary<Guid, int> Balances { get; } = new();
    public List<(string Kind, Guid StockId, int Quantity)> Movements { get; } = [];

    // Returns a result to replace the normal behavior of a movement, or null to apply it.
    public Func<string, Guid, StockMovementResult?> Override { get; set; } = (_, _) => null;

    public (InventoryProduct Product, Guid StockId) AddProduct(
        Guid branchId, int balance, decimal salePrice = 15m, decimal purchasePrice = 10m, string? sku = null)
    {
        var product = new InventoryProduct(Guid.NewGuid(), "Product " + (sku ?? "X"), sku ?? $"SKU-{_products.Count + 1}",
            purchasePrice, salePrice);
        var stockId = Guid.NewGuid();
        _products[product.Id] = product;
        _assignments[(product.Id, branchId)] = stockId;
        Balances[stockId] = balance;
        return (product, stockId);
    }

    public Task<InventoryProduct?> GetProductAsync(Guid productId, CancellationToken cancellationToken) =>
        Task.FromResult(_products.GetValueOrDefault(productId));

    public Task<Guid?> FindStockIdAsync(Guid productId, Guid branchId, CancellationToken cancellationToken) =>
        Task.FromResult<Guid?>(_assignments.TryGetValue((productId, branchId), out var stockId) ? stockId : null);

    public Task<StockMovementResult> IssueAsync(Guid stockId, int quantity, CancellationToken cancellationToken) =>
        Task.FromResult(Move("Issue", stockId, quantity));

    public Task<StockMovementResult> ReceiveAsync(Guid stockId, int quantity, CancellationToken cancellationToken) =>
        Task.FromResult(Move("Receipt", stockId, quantity));

    private StockMovementResult Move(string kind, Guid stockId, int quantity)
    {
        var overridden = Override(kind, stockId);
        if (overridden is not null)
            return overridden;

        if (kind == "Issue" && Balances[stockId] < quantity)
            return StockMovementResult.Rejected(
                $"Insufficient stock. Available: {Balances[stockId]}; requested: {quantity}.");

        Balances[stockId] += kind == "Issue" ? -quantity : quantity;
        Movements.Add((kind, stockId, quantity));
        return StockMovementResult.Applied(Guid.NewGuid());
    }
}
