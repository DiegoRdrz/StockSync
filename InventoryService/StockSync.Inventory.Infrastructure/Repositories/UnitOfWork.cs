using StockSync.Inventory.Domain.Repositories;

namespace StockSync.Inventory.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly InventoryDbContext _context;

    public UnitOfWork(InventoryDbContext context)
    {
        _context = context;
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        if (_context.Database.CurrentTransaction is not null)
            return await operation();

        // On failure, disposal rolls back the transaction in PostgreSQL.
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var result = await operation();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken) =>
        ExecuteInTransactionAsync(async () =>
        {
            await operation();
            return true;
        }, cancellationToken);
}
