using System.Text.Json;

namespace StockSync.Notification.Application.IntegrationEvents;

public enum EventProcessingOutcome
{
    Applied = 1,
    Duplicate = 2
}

public interface IEventProcessor<in T>
{
    Task<EventProcessingOutcome> ProcessAsync(T message, CancellationToken cancellationToken);
}

public sealed class NotificationEventDelivery<T>(IEventProcessor<T> processor, TimeProvider clock)
    where T : class, INotificationEvent<T>
{
    public async Task<bool> ShouldAcknowledgeAsync(
        ReadOnlyMemory<byte> body,
        string key,
        string? type,
        string? id,
        string? tenant,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        T message;
        try
        {
            message = NotificationEventParser.Parse<T>(body, key, type, id, tenant);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            log(ex is JsonException ? "Invalid event JSON or field type." : ex.Message);
            return false;
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var outcome = await processor.ProcessAsync(message, cancellationToken);
                return outcome is EventProcessingOutcome.Applied or EventProcessingOutcome.Duplicate;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                log($"Processing attempt {attempt} failed ({ex.GetType().Name}).");
                if (attempt == 3)
                    return false;

                await Task.Delay(TimeSpan.FromMilliseconds(100 * (1 << (attempt - 1))), clock, cancellationToken);
            }
        }
    }
}
