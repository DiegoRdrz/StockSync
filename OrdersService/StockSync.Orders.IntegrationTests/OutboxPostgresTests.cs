using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StockSync.Orders.Application.IntegrationEvents;
using StockSync.Orders.Domain.Entities;
using StockSync.Orders.Infrastructure;
using StockSync.Orders.Infrastructure.Messaging;
using StockSync.Orders.Infrastructure.Outbox;

namespace StockSync.Orders.IntegrationTests;

public class OutboxPostgresTests : PostgresTestBase
{
    private OutboxPublisher CreatePublisher(IMessagePublisher publisher)
    {
        var services = new ServiceCollection()
            .AddDbContext<OrdersDbContext>(options => options.UseNpgsql(ConnectionString))
            .BuildServiceProvider();
        return new OutboxPublisher(services.GetRequiredService<IServiceScopeFactory>(), publisher,
            NullLogger<OutboxPublisher>.Instance);
    }

    private async Task<List<Guid>> EnqueueAsync(int count)
    {
        await using var context = CreateContext();
        var outbox = new EfIntegrationEventOutbox(context);
        var ids = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            var order = Order.Create(Guid.NewGuid(), null);
            var integrationEvent = OrderCreated.From(order, Guid.NewGuid()) with
            {
                OccurredAt = DateTime.UtcNow.AddSeconds(i)
            };
            outbox.Enqueue(integrationEvent);
            ids.Add(integrationEvent.EventId);
        }
        await context.SaveChangesAsync();
        return ids;
    }

    [PostgreSqlFact]
    public async Task PublishPending_PublishesInOrderAndMarksProcessed()
    {
        var ids = await EnqueueAsync(3);
        var publisher = new RecordingPublisher();

        var published = await CreatePublisher(publisher).PublishPendingAsync(default);

        Assert.Equal(3, published);
        Assert.Equal(ids, publisher.Published);
        await using var context = CreateContext();
        Assert.All(await context.OutboxMessages.ToListAsync(), m => Assert.NotNull(m.ProcessedAt));
        Assert.Equal(0, await CreatePublisher(publisher).PublishPendingAsync(default));
    }

    [PostgreSqlFact]
    public async Task PublishPending_FailureStopsBatchAndKeepsMessagesPending()
    {
        var ids = await EnqueueAsync(3);
        var publisher = new RecordingPublisher { FailOn = ids[1] };

        var published = await CreatePublisher(publisher).PublishPendingAsync(default);

        Assert.Equal(1, published);
        await using var context = CreateContext();
        var messages = await context.OutboxMessages.ToDictionaryAsync(m => m.Id);
        Assert.NotNull(messages[ids[0]].ProcessedAt);
        Assert.Null(messages[ids[1]].ProcessedAt);
        Assert.Equal(1, messages[ids[1]].Attempts);
        Assert.Equal("Broker unavailable.", messages[ids[1]].LastError);
        // A later event is not published before the failed one.
        Assert.Null(messages[ids[2]].ProcessedAt);
        Assert.Equal(0, messages[ids[2]].Attempts);
    }

    private sealed class RecordingPublisher : IMessagePublisher
    {
        public List<Guid> Published { get; } = [];
        public Guid? FailOn { get; init; }

        public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
        {
            if (message.Id == FailOn)
                throw new InvalidOperationException("Broker unavailable.");
            Published.Add(message.Id);
            return Task.CompletedTask;
        }
    }
}
