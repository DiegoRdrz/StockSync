using StockSync.Orders.Application.Common;
using StockSync.Orders.Application.Common.Exceptions;
using StockSync.Orders.Application.IntegrationEvents;
using StockSync.Orders.Application.Inventory;
using StockSync.Orders.Application.Lines;
using StockSync.Orders.Application.Stock;
using StockSync.Orders.Domain.Entities;
using StockSync.Orders.Domain.Repositories;

namespace StockSync.Orders.Application.PurchaseOrders;

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IInventoryClient _inventory;
    private readonly IIntegrationEventOutbox _outbox;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWork _unitOfWork;
    private readonly StockSaga _stockSaga;

    public PurchaseOrderService(
        IPurchaseOrderRepository purchaseOrderRepository,
        IInventoryClient inventory,
        IIntegrationEventOutbox outbox,
        ICurrentTenant currentTenant,
        IUnitOfWork unitOfWork)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
        _inventory = inventory;
        _outbox = outbox;
        _currentTenant = currentTenant;
        _unitOfWork = unitOfWork;
        _stockSaga = new StockSaga(inventory);
    }

    public async Task<PurchaseOrderResponse> CreateAsync(PurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        ValidationErrors.ThrowIfAny(PurchaseOrderValidator.Validate(request));

        var purchaseOrder = PurchaseOrder.Create(request.BranchId, request.SupplierName, request.Notes);
        await _purchaseOrderRepository.AddAsync(purchaseOrder, cancellationToken);
        await _purchaseOrderRepository.SaveChangesAsync(cancellationToken);

        return PurchaseOrderResponse.From(purchaseOrder);
    }

    public async Task<PurchaseOrderResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var purchaseOrder = await _purchaseOrderRepository.GetByIdAsync(id, cancellationToken)
            ?? throw PurchaseOrderNotFound(id);

        return PurchaseOrderResponse.From(purchaseOrder);
    }

    public async Task<PagedResult<PurchaseOrderResponse>> ListAsync(
        PurchaseOrderFilter filter, CancellationToken cancellationToken)
    {
        ValidationErrors.ThrowIfAny(PurchaseOrderValidator.Validate(filter));

        var (items, total) = await _purchaseOrderRepository.ListAsync(
            filter.Status,
            filter.BranchId,
            DocumentFilterValidator.ToUtc(filter.From),
            DocumentFilterValidator.ToUtc(filter.To),
            DocumentFilterValidator.Offset(filter),
            filter.PageSize,
            cancellationToken);

        return new PagedResult<PurchaseOrderResponse>(
            items.Select(PurchaseOrderResponse.From).ToList(), filter.Page, filter.PageSize, total);
    }

    public async Task<PurchaseOrderResponse> AddLineAsync(Guid id, LineRequest request, CancellationToken cancellationToken)
    {
        ValidationErrors.ThrowIfAny(LineValidator.Validate(request));

        // Check the purchase order before calling Inventory; the domain checks again under the row lock.
        var current = await _purchaseOrderRepository.GetByIdAsync(id, cancellationToken)
            ?? throw PurchaseOrderNotFound(id);
        if (current.Status != PurchaseOrderStatus.Draft)
            throw new ConflictException(
                $"Lines can only be added to a draft purchase order. Current status: {current.Status}.");

        var source = await LineSource.LoadAsync(
            _inventory, request.ProductId, current.BranchId, p => p.PurchasePrice, "purchase price", cancellationToken);

        return await ChangeAsync(id, purchaseOrder => purchaseOrder.AddLine(
            source.Product.Id,
            source.StockId,
            source.Product.Name,
            source.Product.Sku,
            request.Quantity,
            source.UnitPrice), cancellationToken);
    }

    public Task<PurchaseOrderResponse> UpdateLineAsync(
        Guid id, Guid lineId, LineQuantityRequest request, CancellationToken cancellationToken)
    {
        ValidationErrors.ThrowIfAny(LineValidator.Validate(request));

        return ChangeAsync(id, purchaseOrder =>
        {
            EnsureLineExists(purchaseOrder, lineId);
            purchaseOrder.UpdateLineQuantity(lineId, request.Quantity);
        }, cancellationToken);
    }

    public Task<PurchaseOrderResponse> RemoveLineAsync(Guid id, Guid lineId, CancellationToken cancellationToken) =>
        ChangeAsync(id, purchaseOrder =>
        {
            EnsureLineExists(purchaseOrder, lineId);
            purchaseOrder.RemoveLine(lineId);
        }, cancellationToken);

    public Task<PurchaseOrderResponse> PlaceAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, purchaseOrder => purchaseOrder.Place(), cancellationToken);

    public Task<PurchaseOrderResponse> CancelAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, purchaseOrder => purchaseOrder.Cancel(), cancellationToken);

    // Same saga as sales confirmation, recording receipts instead of issues.
    public async Task<PurchaseOrderResponse> ReceiveAsync(Guid id, CancellationToken cancellationToken)
    {
        var purchaseOrder = await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var locked = await _purchaseOrderRepository.GetForUpdateAsync(id, cancellationToken)
                ?? throw PurchaseOrderNotFound(id);
            locked.StartReceiving();
            await _purchaseOrderRepository.SaveChangesAsync(cancellationToken);
            return locked;
        }, cancellationToken);

        // From here on the request token is ignored: stopping halfway would leave stock movements
        // without a recorded result.
        var result = await _stockSaga.ExecuteAsync(
            purchaseOrder, StockDirection.Receipt,
            () => _purchaseOrderRepository.SaveChangesAsync(CancellationToken.None));

        switch (result.Outcome)
        {
            case StockSagaOutcome.Completed:
                purchaseOrder.CompleteReceiving();
                _outbox.Enqueue(PurchaseOrderReceived.From(purchaseOrder, _currentTenant.TenantId));
                await _purchaseOrderRepository.SaveChangesAsync(CancellationToken.None);
                return PurchaseOrderResponse.From(purchaseOrder);

            case StockSagaOutcome.Reverted:
                purchaseOrder.RevertToOrdered();
                await _purchaseOrderRepository.SaveChangesAsync(CancellationToken.None);
                throw result.InventoryUnavailable
                    ? new InventoryUnavailableException($"The purchase order was not received and stock was not changed. {result.Error}")
                    : new ConflictException($"The purchase order was not received and stock was not changed. {result.Error}");

            default:
                purchaseOrder.RequireReview(result.Error!);
                await _purchaseOrderRepository.SaveChangesAsync(CancellationToken.None);
                throw new InventoryUnavailableException(
                    $"The stock result is unknown; purchase order '{id}' requires review. {result.Error}");
        }
    }

    // Load and lock the purchase order, apply a domain change and save it in one transaction.
    private Task<PurchaseOrderResponse> ChangeAsync(
        Guid id, Action<PurchaseOrder> change, CancellationToken cancellationToken) =>
        _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var purchaseOrder = await _purchaseOrderRepository.GetForUpdateAsync(id, cancellationToken)
                ?? throw PurchaseOrderNotFound(id);
            change(purchaseOrder);
            await _purchaseOrderRepository.SaveChangesAsync(cancellationToken);
            return PurchaseOrderResponse.From(purchaseOrder);
        }, cancellationToken);

    private static void EnsureLineExists(PurchaseOrder purchaseOrder, Guid lineId)
    {
        if (purchaseOrder.Lines.All(l => l.Id != lineId))
            throw new NotFoundException($"Line '{lineId}' was not found in purchase order '{purchaseOrder.Id}'.");
    }

    private static NotFoundException PurchaseOrderNotFound(Guid id) => new($"Purchase order '{id}' was not found.");
}
