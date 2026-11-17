using StockSync.Orders.Application.Common;
using StockSync.Orders.Application.Common.Exceptions;
using StockSync.Orders.Application.IntegrationEvents;
using StockSync.Orders.Application.Inventory;
using StockSync.Orders.Application.Lines;
using StockSync.Orders.Application.Stock;
using StockSync.Orders.Domain.Entities;
using StockSync.Orders.Domain.Repositories;

namespace StockSync.Orders.Application.Orders;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IInventoryClient _inventory;
    private readonly IIntegrationEventOutbox _outbox;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWork _unitOfWork;
    private readonly StockSaga _stockSaga;

    public OrderService(
        IOrderRepository orderRepository,
        IInventoryClient inventory,
        IIntegrationEventOutbox outbox,
        ICurrentTenant currentTenant,
        IUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _inventory = inventory;
        _outbox = outbox;
        _currentTenant = currentTenant;
        _unitOfWork = unitOfWork;
        _stockSaga = new StockSaga(inventory);
    }

    public async Task<OrderResponse> CreateAsync(OrderRequest request, CancellationToken cancellationToken)
    {
        ValidationErrors.ThrowIfAny(OrderValidator.Validate(request));

        var order = Order.Create(request.BranchId, request.Notes);
        await _orderRepository.AddAsync(order, cancellationToken);
        _outbox.Enqueue(OrderCreated.From(order, _currentTenant.TenantId));
        // One SaveChanges stores the order and its event atomically.
        await _orderRepository.SaveChangesAsync(cancellationToken);

        return OrderResponse.From(order);
    }

    public async Task<OrderResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(id, cancellationToken)
            ?? throw OrderNotFound(id);

        return OrderResponse.From(order);
    }

    public async Task<PagedResult<OrderResponse>> ListAsync(OrderFilter filter, CancellationToken cancellationToken)
    {
        ValidationErrors.ThrowIfAny(OrderValidator.Validate(filter));

        var (items, total) = await _orderRepository.ListAsync(
            filter.Status,
            filter.BranchId,
            DocumentFilterValidator.ToUtc(filter.From),
            DocumentFilterValidator.ToUtc(filter.To),
            DocumentFilterValidator.Offset(filter),
            filter.PageSize,
            cancellationToken);

        return new PagedResult<OrderResponse>(
            items.Select(OrderResponse.From).ToList(), filter.Page, filter.PageSize, total);
    }

    public async Task<OrderResponse> AddLineAsync(Guid id, LineRequest request, CancellationToken cancellationToken)
    {
        ValidationErrors.ThrowIfAny(LineValidator.Validate(request));

        // Check the order before calling Inventory; the domain checks again under the row lock.
        var current = await _orderRepository.GetByIdAsync(id, cancellationToken)
            ?? throw OrderNotFound(id);
        if (current.Status != OrderStatus.Draft)
            throw new ConflictException($"Lines can only be added to a draft order. Current status: {current.Status}.");

        var source = await LineSource.LoadAsync(
            _inventory, request.ProductId, current.BranchId, p => p.SalePrice, "sale price", cancellationToken);

        return await ChangeAsync(id, order => order.AddLine(
            source.Product.Id,
            source.StockId,
            source.Product.Name,
            source.Product.Sku,
            request.Quantity,
            source.UnitPrice), cancellationToken);
    }

    public Task<OrderResponse> UpdateLineAsync(
        Guid id, Guid lineId, LineQuantityRequest request, CancellationToken cancellationToken)
    {
        ValidationErrors.ThrowIfAny(LineValidator.Validate(request));

        return ChangeAsync(id, order =>
        {
            EnsureLineExists(order, lineId);
            order.UpdateLineQuantity(lineId, request.Quantity);
        }, cancellationToken);
    }

    public Task<OrderResponse> RemoveLineAsync(Guid id, Guid lineId, CancellationToken cancellationToken) =>
        ChangeAsync(id, order =>
        {
            EnsureLineExists(order, lineId);
            order.RemoveLine(lineId);
        }, cancellationToken);

    public Task<OrderResponse> CancelAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, order => order.Cancel(), cancellationToken);

    // Confirmation is a saga across two databases:
    // 1. Draft -> Confirming is committed first; it blocks edits and a second confirmation.
    // 2. Inventory issues the stock of each line; progress is saved after every call.
    // 3. The order ends Confirmed, back in Draft (stock reverted) or RequiresReview (unknown result).
    public async Task<OrderResponse> ConfirmAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var locked = await _orderRepository.GetForUpdateAsync(id, cancellationToken)
                ?? throw OrderNotFound(id);
            locked.StartConfirmation();
            await _orderRepository.SaveChangesAsync(cancellationToken);
            return locked;
        }, cancellationToken);

        // From here on the request token is ignored: stopping halfway would leave stock movements
        // without a recorded result.
        var result = await _stockSaga.ExecuteAsync(
            order, StockDirection.Issue, () => _orderRepository.SaveChangesAsync(CancellationToken.None));

        switch (result.Outcome)
        {
            case StockSagaOutcome.Completed:
                order.CompleteConfirmation();
                _outbox.Enqueue(SaleConfirmed.From(order, _currentTenant.TenantId));
                await _orderRepository.SaveChangesAsync(CancellationToken.None);
                return OrderResponse.From(order);

            case StockSagaOutcome.Reverted:
                order.RevertToDraft();
                await _orderRepository.SaveChangesAsync(CancellationToken.None);
                throw result.InventoryUnavailable
                    ? new InventoryUnavailableException($"The sale was not confirmed and stock was not changed. {result.Error}")
                    : new ConflictException($"The sale was not confirmed and stock was not changed. {result.Error}");

            default:
                order.RequireReview(result.Error!);
                await _orderRepository.SaveChangesAsync(CancellationToken.None);
                throw new InventoryUnavailableException(
                    $"The stock result is unknown; order '{id}' requires review. {result.Error}");
        }
    }

    // Load and lock the order, apply a domain change and save it in one transaction.
    private Task<OrderResponse> ChangeAsync(Guid id, Action<Order> change, CancellationToken cancellationToken) =>
        _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var order = await _orderRepository.GetForUpdateAsync(id, cancellationToken)
                ?? throw OrderNotFound(id);
            change(order);
            await _orderRepository.SaveChangesAsync(cancellationToken);
            return OrderResponse.From(order);
        }, cancellationToken);

    private static void EnsureLineExists(Order order, Guid lineId)
    {
        if (order.Lines.All(l => l.Id != lineId))
            throw new NotFoundException($"Line '{lineId}' was not found in order '{order.Id}'.");
    }

    private static NotFoundException OrderNotFound(Guid id) => new($"Order '{id}' was not found.");
}
