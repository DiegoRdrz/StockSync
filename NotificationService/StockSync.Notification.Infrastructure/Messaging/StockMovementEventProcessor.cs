using StockSync.Notification.Application.Common;
using StockSync.Notification.Application.IntegrationEvents;

namespace StockSync.Notification.Infrastructure.Messaging;

public sealed class StockMovementEventProcessor(
    NotificationDbContext db,
    TenantContext tenant,
    NotificationDispatcher dispatcher)
    : TransactionalEventProcessor<StockMovementRecordedEvent>(db, tenant)
{
    protected override Task HandleEventAsync(StockMovementRecordedEvent message, CancellationToken cancellationToken) =>
        dispatcher.DispatchLowStockIfTriggeredAsync(message, cancellationToken);
}
