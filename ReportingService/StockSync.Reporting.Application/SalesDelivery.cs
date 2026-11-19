using System.Text.Json;

namespace StockSync.Reporting.Application;

public enum ProjectionResult { Applied, Duplicate }
public interface ISalesProjection
{
    Task<ProjectionResult> ApplyAsync(SaleConfirmed sale, CancellationToken token);
}

public sealed class SalesDelivery(ISalesProjection projection, TimeProvider clock)
{
    public async Task<bool> ShouldAcknowledgeAsync(ReadOnlyMemory<byte> body, string key, string? type,
        string? id, string? tenant, Action<string> log, CancellationToken token)
    {
        SaleConfirmed sale;
        try
        {
            sale = SaleConfirmed.Parse(body, key, type, id, tenant);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            log(ex is JsonException ? "Invalid sales JSON or field type." : ex.Message);
            return false;
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var outcome = await projection.ApplyAsync(sale, token);
                return outcome is ProjectionResult.Applied or ProjectionResult.Duplicate;
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                log($"Sales projection attempt {attempt} failed ({ex.GetType().Name}).");
                if (attempt == 3) return false;
                await Task.Delay(TimeSpan.FromMilliseconds(100 * (1 << (attempt - 1))), clock, token);
            }
        }
    }
}
