using StockSync.Inventory.Infrastructure.Outbox;

namespace StockSync.Inventory.Infrastructure.Messaging;

public interface IMessagePublisher
{
    // Completes only after the broker confirms the message.
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken);
}
