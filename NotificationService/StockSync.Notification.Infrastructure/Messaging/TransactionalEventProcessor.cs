using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockSync.Notification.Application.Common;
using StockSync.Notification.Application.IntegrationEvents;

namespace StockSync.Notification.Infrastructure.Messaging;

public abstract class TransactionalEventProcessor<T>(NotificationDbContext db, TenantContext tenant)
    : IEventProcessor<T>
    where T : INotificationEvent<T>
{
    protected NotificationDbContext Db { get; } = db;

    protected abstract Task HandleEventAsync(T message, CancellationToken cancellationToken);

    public async Task<EventProcessingOutcome> ProcessAsync(T message, CancellationToken cancellationToken)
    {
        tenant.Set(message.TenantId);
        Db.ChangeTracker.Clear();

        await using var transaction = await Db.Database.BeginTransactionAsync(cancellationToken);
        Db.ProcessedEvents.Add(new ProcessedEvent(T.ConsumerName, message.EventId, DateTime.UtcNow));

        try
        {
            await Db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "PK_ProcessedEvents" })
        {
            await transaction.RollbackAsync(cancellationToken);
            return EventProcessingOutcome.Duplicate;
        }

        await HandleEventAsync(message, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return EventProcessingOutcome.Applied;
    }
}
