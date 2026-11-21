namespace StockSync.Notification.Infrastructure;

public sealed record ProcessedEvent(string ConsumerName, Guid EventId, DateTime ProcessedAt);
