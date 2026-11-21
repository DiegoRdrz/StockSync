using System.Text.Json;
using StockSync.Inventory.Application.IntegrationEvents;

namespace StockSync.Inventory.Infrastructure.Outbox;

// Integration event stored with the business change and published later by OutboxPublisher.
public class OutboxMessage
{
    public const int EventTypeMaxLength = 100;
    public const int LastErrorMaxLength = 1000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Same value as the event ID, so consumers can deduplicate with the AMQP message ID.
    public Guid Id { get; private set; }
    public string EventType { get; private set; } = null!;
    public Guid TenantId { get; private set; }
    public string Payload { get; private set; } = null!;
    public DateTime OccurredAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }

    private OutboxMessage()
    {
    }

    public static OutboxMessage From(IIntegrationEvent integrationEvent) => new()
    {
        Id = integrationEvent.EventId,
        EventType = integrationEvent.EventType,
        TenantId = integrationEvent.TenantId,
        // Serialize the concrete event, not the interface, so every field is included.
        Payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), JsonOptions),
        OccurredAt = integrationEvent.OccurredAt
    };

    public void MarkProcessed(DateTime processedAt)
    {
        ProcessedAt = processedAt;
        Attempts++;
        LastError = null;
    }

    public void MarkFailed(string error)
    {
        Attempts++;
        LastError = error.Length <= LastErrorMaxLength ? error : error[..LastErrorMaxLength];
    }
}
