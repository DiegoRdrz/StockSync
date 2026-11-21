using StockSync.Notification.Application.Common;
using StockSync.Notification.Application.IntegrationEvents;

namespace StockSync.Notification.Infrastructure.Messaging;

public sealed class OrderCreatedEventProcessor(
    NotificationDbContext db,
    TenantContext tenant,
    NotificationDispatcher dispatcher)
    : TransactionalEventProcessor<OrderCreatedEvent>(db, tenant)
{
    protected override Task HandleEventAsync(OrderCreatedEvent message, CancellationToken cancellationToken) =>
        dispatcher.DispatchOrderCreatedAsync(message, cancellationToken);
}
