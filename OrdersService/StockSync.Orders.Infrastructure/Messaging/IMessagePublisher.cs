using StockSync.Orders.Infrastructure.Outbox;

namespace StockSync.Orders.Infrastructure.Messaging;

public interface IMessagePublisher
{
    // Completes only after the broker confirms the message.
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken);
}
