using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using StockSync.Notification.Application.Common;
using StockSync.Notification.Application.IntegrationEvents;
using StockSync.Notification.Infrastructure;
using StockSync.Notification.Infrastructure.Messaging;

namespace StockSync.Notification.IntegrationTests;

public sealed class RabbitMqFactAttribute : FactAttribute
{
    public RabbitMqFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STOCKSYNC_TEST_RABBITMQ")))
            Skip = "Set STOCKSYNC_TEST_RABBITMQ to run against RabbitMQ.";
    }
}

public class RabbitMqConsumerTests
{
    [RabbitMqFact]
    public Task InvalidOrderEventReachesItsErrorQueue() =>
        RejectInvalidDeliveryAsync<OrderCreatedEvent>();

    [RabbitMqFact]
    public Task InvalidStockMovementEventReachesItsErrorQueue() =>
        RejectInvalidDeliveryAsync<StockMovementRecordedEvent>();

    private static async Task RejectInvalidDeliveryAsync<T>()
        where T : class, INotificationEvent<T>
    {
        var broker = new Uri(Environment.GetEnvironmentVariable("STOCKSYNC_TEST_RABBITMQ")!);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var token = timeout.Token;

        await using var connection = await new ConnectionFactory { Uri = broker }.CreateConnectionAsync(token);
        await using var producer = await connection.CreateChannelAsync(cancellationToken: token);
        await using var subscription = await connection.CreateChannelAsync(cancellationToken: token);
        await using var services = new ServiceCollection()
            .AddSingleton(TimeProvider.System)
            .AddScoped<TenantContext>()
            .AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<TenantContext>())
            .AddDbContext<NotificationDbContext>(o => o.UseNpgsql())
            .AddScoped<IEventProcessor<T>, NoOpProcessor<T>>()
            .AddScoped<NotificationEventDelivery<T>>()
            .BuildServiceProvider();

        var worker = new NotificationConsumer<T>(
            broker,
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<NotificationConsumer<T>>.Instance);

        var queue = $"notification.test.{Guid.NewGuid():N}";
        var exchange = queue + ".events";
        var dlx = queue + ".dlx";
        var errorQueue = queue + ".error";

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        await NotificationConsumer<T>.DeclareAsync(producer, queue, token, exchange, dlx);
        var run = worker.ConsumeAsync(subscription, queue, stop.Token, exchange, dlx);

        try
        {
            var body = "invalid-payload"u8.ToArray();
            await producer.BasicPublishAsync("", queue, body, token);

            BasicGetResult? rejected;
            while ((rejected = await producer.BasicGetAsync(errorQueue, true, token)) is null)
            {
                if (run.IsCompleted)
                    await run;
                await Task.Delay(50, token);
            }

            Assert.Equal(body, rejected.Body.ToArray());
            Assert.True(rejected.BasicProperties.Headers!.ContainsKey("x-death"));
        }
        finally
        {
            stop.Cancel();
            try
            {
                await run;
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
            }

            await producer.QueueDeleteAsync(queue, false, false);
            await producer.QueueDeleteAsync(errorQueue, false, false);
            await producer.ExchangeDeleteAsync(exchange, cancellationToken: token);
            await producer.ExchangeDeleteAsync(dlx, cancellationToken: token);
        }
    }

    private sealed class NoOpProcessor<T> : IEventProcessor<T>
    {
        public Task<EventProcessingOutcome> ProcessAsync(T message, CancellationToken cancellationToken) =>
            Task.FromResult(EventProcessingOutcome.Applied);
    }
}
