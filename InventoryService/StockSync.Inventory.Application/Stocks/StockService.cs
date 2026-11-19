using StockSync.Inventory.Application.Common;
using StockSync.Inventory.Application.Common.Exceptions;
using StockSync.Inventory.Application.IntegrationEvents;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Repositories;

namespace StockSync.Inventory.Application.Stocks;

public class StockService : IStockService
{
    private readonly IStockRepository _stockRepository;
    private readonly IProductRepository _productRepository;
    private readonly IStockMovementRepository _movementRepository;
    private readonly IIntegrationEventOutbox _outbox;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWork _unitOfWork;

    public StockService(
        IStockRepository stockRepository,
        IProductRepository productRepository,
        IStockMovementRepository movementRepository,
        IIntegrationEventOutbox outbox,
        ICurrentTenant currentTenant,
        IUnitOfWork unitOfWork)
    {
        _stockRepository = stockRepository;
        _productRepository = productRepository;
        _movementRepository = movementRepository;
        _outbox = outbox;
        _currentTenant = currentTenant;
        _unitOfWork = unitOfWork;
    }

    public async Task<StockResponse> CreateAsync(StockRequest request, CancellationToken cancellationToken)
    {
        ThrowIfErrors(StockValidator.Validate(request));

        // An invalid body reference is a validation error (400), not a missing URL resource (404).
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(StockRequest.ProductId)] = [$"Product '{request.ProductId}' does not exist or is inactive."]
            });

        if (await _stockRepository.ExistsAsync(request.ProductId, request.BranchId, cancellationToken))
            throw new ConflictException(
                $"Product '{request.ProductId}' is already assigned to branch '{request.BranchId}'.");

        // Record the initial balance as an adjustment from zero to preserve its history.
        var stock = Stock.Create(request.ProductId, request.BranchId, 0);
        var initialBalance = stock.UpdateQuantity(request.Quantity);

        await _stockRepository.AddAsync(stock, cancellationToken);
        if (initialBalance is not null)
        {
            await _movementRepository.AddAsync(initialBalance, cancellationToken);
            _outbox.Enqueue(StockMovementRecorded.From(initialBalance, stock, product, _currentTenant.TenantId));
        }
        await _stockRepository.SaveChangesAsync(cancellationToken);

        return StockResponse.From(stock);
    }

    public async Task<StockResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var stock = await _stockRepository.GetByIdAsync(id, cancellationToken)
            ?? throw StockNotFound(id);

        return StockResponse.From(stock);
    }

    public async Task<PagedResult<StockResponse>> ListByBranchAsync(
        Guid branchId, StockFilter filter, CancellationToken cancellationToken)
    {
        ThrowIfErrors(StockValidator.Validate(filter));

        var (items, total) = await _stockRepository.ListByBranchAsync(
            branchId, Offset(filter), filter.PageSize, cancellationToken);

        return new PagedResult<StockResponse>(
            items.Select(StockResponse.From).ToList(), filter.Page, filter.PageSize, total);
    }

    public async Task<PagedResult<StockResponse>> ListByProductAsync(
        Guid productId, StockFilter filter, CancellationToken cancellationToken)
    {
        ThrowIfErrors(StockValidator.Validate(filter));

        if (await _productRepository.GetByIdAsync(productId, cancellationToken) is null)
            throw new NotFoundException($"Product '{productId}' was not found.");

        var (items, total) = await _stockRepository.ListByProductAsync(
            productId, Offset(filter), filter.PageSize, cancellationToken);

        return new PagedResult<StockResponse>(
            items.Select(StockResponse.From).ToList(), filter.Page, filter.PageSize, total);
    }

    public async Task<PagedResult<LowStockResponse>> ListLowStockAsync(
        LowStockFilter filter, CancellationToken cancellationToken)
    {
        ThrowIfErrors(StockValidator.Validate(filter));

        var (items, total) = await _stockRepository.ListLowStockAsync(
            filter.BranchId, Offset(filter), filter.PageSize, cancellationToken);

        return new PagedResult<LowStockResponse>(
            items.Select(LowStockResponse.From).ToList(), filter.Page, filter.PageSize, total);
    }

    public Task<StockResponse> UpdateQuantityAsync(Guid id, StockQuantityRequest request, CancellationToken cancellationToken)
    {
        ThrowIfErrors(StockValidator.Validate(request));

        return _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var stock = await _stockRepository.GetForUpdateAsync(id, cancellationToken)
                ?? throw StockNotFound(id);
            var product = await _productRepository.GetByIdAsync(stock.ProductId, cancellationToken)
                ?? throw new ConflictException($"Product '{stock.ProductId}' is inactive; its stock cannot be changed.");

            var adjustment = stock.UpdateQuantity(request.NewQuantity);
            if (adjustment is not null)
            {
                await _movementRepository.AddAsync(adjustment, cancellationToken);
                _outbox.Enqueue(StockMovementRecorded.From(adjustment, stock, product, _currentTenant.TenantId));
            }
            await _stockRepository.SaveChangesAsync(cancellationToken);

            return StockResponse.From(stock);
        }, cancellationToken);
    }

    // Delete only the branch assignment; leave the product unchanged.
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var stock = await _stockRepository.GetForUpdateAsync(id, cancellationToken)
                ?? throw StockNotFound(id);

            // Deleting an assignment with units would erase stock without an explanatory movement.
            if (stock.Quantity > 0)
                throw new ConflictException("Cannot delete the assignment because it has stock.");

            _stockRepository.Delete(stock);
            await _stockRepository.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

    private static int Offset(StockFilter filter) => (filter.Page - 1) * filter.PageSize;

    private static void ThrowIfErrors(Dictionary<string, string[]> errors)
    {
        if (errors.Count > 0)
            throw new ValidationException(errors);
    }

    private static NotFoundException StockNotFound(Guid id) =>
        new($"Stock '{id}' was not found.");
}
