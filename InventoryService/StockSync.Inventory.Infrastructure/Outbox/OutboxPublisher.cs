using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StockSync.Inventory.Infrastructure.Messaging;

namespace StockSync.Inventory.Infrastructure.Outbox;

// Publishes pending outbox messages to the broker. Delivery is at least once: if the process
// stops after publishing but before saving ProcessedAt, the message is published again.
public sealed class OutboxPublisher : BackgroundService
{
    public const int BatchSize = 50;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<OutboxPublisher> _logger;

    public OutboxPublisher(IServiceScopeFactory scopeFactory, IMessagePublisher publisher, ILogger<OutboxPublisher> logger)
    {
        _scopeFactory = scopeFactory;
        _publisher = publisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var published = 0;
            try
            {
                published = await PublishPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Outbox publishing failed; pending messages will be retried.");
            }

            // A full batch suggests more pending messages, so continue without waiting.
            if (published == BatchSize)
                continue;

            try
            {
                await Task.Delay(PollingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    // Publish pending messages in the order they occurred. A failure stops the batch so a later
    // event is never published before an earlier one. Returns the number of published messages.
    public async Task<int> PublishPendingAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        // SKIP LOCKED lets several replicas share the outbox without publishing the same row twice at once.
        var messages = await context.OutboxMessages
            .FromSqlInterpolated($"""
                SELECT * FROM "OutboxMessages"
                WHERE "ProcessedAt" IS NULL
                ORDER BY "OccurredAt", "Id"
                LIMIT {BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        var published = 0;
        foreach (var message in messages)
        {
            try
            {
                await _publisher.PublishAsync(message, cancellationToken);
                message.MarkProcessed(DateTime.UtcNow);
                published++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.MarkFailed(ex.Message);
                _logger.LogWarning(ex, "Could not publish outbox message {MessageId} ({EventType}).",
                    message.Id, message.EventType);
                break;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return published;
    }
}
