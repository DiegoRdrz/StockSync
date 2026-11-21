using StockSync.Notification.Domain.Repositories;

namespace StockSync.Notification.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly NotificationDbContext _context;

    public UnitOfWork(NotificationDbContext context)
    {
        _context = context;
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        if (_context.Database.CurrentTransaction is not null)
            return await operation();

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
