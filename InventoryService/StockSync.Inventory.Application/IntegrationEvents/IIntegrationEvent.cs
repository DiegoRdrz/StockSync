using System.Text.Json.Serialization;

namespace StockSync.Inventory.Application.IntegrationEvents;

// Event published to other services through the outbox and RabbitMQ.
// Consumers must deduplicate by EventId because delivery is at least once.
public interface IIntegrationEvent
{
    Guid EventId { get; }

    DateTime OccurredAt { get; }

    Guid TenantId { get; }

    // RabbitMQ routing key. The version suffix lets a contract evolve without breaking consumers.
    [JsonIgnore]
    string EventType { get; }
}

// Stores events in the same transaction as the business change (transactional outbox).
// The event is saved by the next repository SaveChangesAsync and published afterwards.
public interface IIntegrationEventOutbox
{
    void Enqueue(IIntegrationEvent integrationEvent);
}
