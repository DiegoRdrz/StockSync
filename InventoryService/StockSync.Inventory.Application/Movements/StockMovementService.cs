using StockSync.Inventory.Application.Common;
using StockSync.Inventory.Application.Common.Exceptions;
using StockSync.Inventory.Application.IntegrationEvents;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Repositories;

namespace StockSync.Inventory.Application.Movements;

public class StockMovementService : IStockMovementService
{
    private readonly IStockRepository _stockRepository;
    private readonly IProductRepository _productRepository;
    private readonly IStockMovementRepository _movementRepository;
    private readonly IIntegrationEventOutbox _outbox;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWork _unitOfWork;

    public StockMovementService(IStockRepository stockRepository, IProductRepository productRepository,
        IStockMovementRepository movementRepository, IIntegrationEventOutbox outbox,
        ICurrentTenant currentTenant, IUnitOfWork unitOfWork)
    {
        _stockRepository = stockRepository;
        _productRepository = productRepository;
        _movementRepository = movementRepository;
        _outbox = outbox;
        _currentTenant = currentTenant;
        _unitOfWork = unitOfWork;
    }

    public Task<StockMovementResponse> RecordReceiptAsync(Guid stockId, StockMovementRequest request, CancellationToken cancellationToken) =>
        RecordAsync(stockId, StockMovementType.Receipt, request, cancellationToken);

    public Task<StockMovementResponse> RecordIssueAsync(Guid stockId, StockMovementRequest request, CancellationToken cancellationToken) =>
        RecordAsync(stockId, StockMovementType.Issue, request, cancellationToken);

    private Task<StockMovementResponse> RecordAsync(Guid stockId, StockMovementType type,
        StockMovementRequest request, CancellationToken cancellationToken)
    {
        ThrowIfErrors(StockMovementValidator.Validate(request));

        // With the row locked, concurrent sales use the committed balance in sequence
        // rather than rejecting the second sale because of a concurrency conflict.
        return _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var stock = await _stockRepository.GetForUpdateAsync(stockId, cancellationToken)
                ?? throw StockNotFound(stockId);

            var product = await _productRepository.GetByIdAsync(stock.ProductId, cancellationToken)
                ?? throw new ConflictException($"Product '{stock.ProductId}' is inactive; its stock cannot record movements.");

            var movement = stock.RecordMovement(type, request.Quantity);
            await _movementRepository.AddAsync(movement, cancellationToken);

            // The shared DbContext commits the balance, movement and event together.
            _outbox.Enqueue(StockMovementRecorded.From(movement, stock, product, _currentTenant.TenantId));
            await _stockRepository.SaveChangesAsync(cancellationToken);
            return StockMovementResponse.From(movement);
        }, cancellationToken);
    }

    public async Task<StockMovementResponse> GetByIdAsync(Guid stockId, Guid id, CancellationToken cancellationToken)
    {
        var movement = await _movementRepository.GetByIdAsync(stockId, id, cancellationToken)
            ?? throw new NotFoundException($"Movement '{id}' was not found for stock '{stockId}'.");
        return StockMovementResponse.From(movement);
    }

    public async Task<PagedResult<StockMovementResponse>> ListAsync(
        Guid stockId, StockMovementFilter filter, CancellationToken cancellationToken)
    {
        ThrowIfErrors(StockMovementValidator.Validate(filter));
        _ = await _stockRepository.GetByIdAsync(stockId, cancellationToken)
            ?? throw StockNotFound(stockId);

        var (items, total) = await _movementRepository.ListAsync(
            stockId, (filter.Page - 1) * filter.PageSize, filter.PageSize, cancellationToken);
        return new PagedResult<StockMovementResponse>(
            items.Select(StockMovementResponse.From).ToList(), filter.Page, filter.PageSize, total);
    }

    public async Task<PagedResult<StockMovementListResponse>> ListAllAsync(
        int page, CancellationToken cancellationToken)
    {
        const int pageSize = 10;
        ThrowIfErrors(StockMovementValidator.Validate(new StockMovementFilter(page, pageSize)));
        var (items, total) = await _movementRepository.ListAllAsync(
            (page - 1) * pageSize, pageSize, cancellationToken);
        return new PagedResult<StockMovementListResponse>(
            items.Select(StockMovementListResponse.From).ToList(), page, pageSize, total);
    }

    private static void ThrowIfErrors(Dictionary<string, string[]> errors)
    {
        if (errors.Count > 0)
            throw new ValidationException(errors);
    }

    private static NotFoundException StockNotFound(Guid id) => new($"Stock '{id}' was not found.");
}
