using StockSync.Inventory.Domain.Repositories;

namespace StockSync.Inventory.UnitTests.Application;

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
