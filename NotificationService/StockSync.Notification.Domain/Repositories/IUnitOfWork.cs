namespace StockSync.Notification.Domain.Repositories;

// Repositories share the request context; this unit defines the transaction boundary
// so row locks and processed-event deduplication remain atomic until the operation commits.
public interface IUnitOfWork
{
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken);

    Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken);
}
