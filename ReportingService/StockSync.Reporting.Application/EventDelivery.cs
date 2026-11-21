using System.Text.Json;

namespace StockSync.Reporting.Application;

public enum ProjectionResult { Applied, Duplicate }
public interface IProjection<in T>
{
    Task<ProjectionResult> ApplyAsync(T message, CancellationToken token);
}

public sealed class EventDelivery<T>(IProjection<T> projection, TimeProvider clock) where T : class, IReportingEvent<T>
{
    public async Task<bool> ShouldAcknowledgeAsync(ReadOnlyMemory<byte> body, string key, string? type,
        string? id, string? tenant, Action<string> log, CancellationToken token)
    {
        T message;
        try
        {
            message = ReportingEvent.Parse<T>(body, key, type, id, tenant);
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
                var outcome = await projection.ApplyAsync(message, token);
                return outcome is ProjectionResult.Applied or ProjectionResult.Duplicate;
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                log($"Projection attempt {attempt} failed ({ex.GetType().Name}).");
                if (attempt == 3) return false;
                await Task.Delay(TimeSpan.FromMilliseconds(100 * (1 << (attempt - 1))), clock, token);
            }
        }
    }
}
