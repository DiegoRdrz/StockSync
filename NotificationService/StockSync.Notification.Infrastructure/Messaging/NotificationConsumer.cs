using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StockSync.Notification.Application.IntegrationEvents;

namespace StockSync.Notification.Infrastructure.Messaging;

public sealed class NotificationConsumer<T>(
    Uri broker,
    IServiceScopeFactory scopes,
    ILogger<NotificationConsumer<T>> logger) : BackgroundService
    where T : class, INotificationEvent<T>
{
    public const string Exchange = "stocksync.events";
    public const string DeadLetterExchange = "notification.dlx";

    public static async Task DeclareAsync(
        IChannel channel,
        string queue,
        CancellationToken cancellationToken,
        string exchange = Exchange,
        string deadLetterExchange = DeadLetterExchange)
    {
        await channel.ExchangeDeclareAsync(
            exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(
            deadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: cancellationToken);

        var errorQueue = queue + ".error";
        await channel.QueueDeclareAsync(
            errorQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            errorQueue, deadLetterExchange, errorQueue, cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = deadLetterExchange,
                ["x-dead-letter-routing-key"] = errorQueue
            },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(queue, exchange, T.RoutingKey, cancellationToken: cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            Uri = broker,
            AutomaticRecoveryEnabled = true,
            ConsumerDispatchConcurrency = 1,
            ClientProvidedName = $"stocksync-{T.ConsumerName}"
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await factory.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                await ConsumeAsync(channel, T.ConsumerName, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Subscription {Queue} interrupted ({Reason}).", T.ConsumerName, ex.GetType().Name);
            }

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }

    public async Task ConsumeAsync(
        IChannel channel,
        string queue,
        CancellationToken cancellationToken,
        string exchange = Exchange,
        string deadLetterExchange = DeadLetterExchange)
    {
        await DeclareAsync(channel, queue, cancellationToken, exchange, deadLetterExchange);
        await channel.BasicQosAsync(0, 1, false, cancellationToken);

        // A single reader owns settlement and shutdown. Copy the body before the callback returns.
        var inbox = System.Threading.Channels.Channel.CreateUnbounded<Delivery>();
        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += (_, args) =>
        {
            object? header = null;
            args.BasicProperties.Headers?.TryGetValue("tenant-id", out header);
            var tenant = header is byte[] bytes ? Encoding.UTF8.GetString(bytes) : header as string;
            inbox.Writer.TryWrite(new Delivery(
                args.DeliveryTag,
                args.Body.ToArray(),
                args.RoutingKey,
                args.BasicProperties.Type,
                args.BasicProperties.MessageId,
                tenant));
            return Task.CompletedTask;
        };

        consumer.UnregisteredAsync += (_, _) =>
        {
            inbox.Writer.TryComplete();
            return Task.CompletedTask;
        };

        channel.ChannelShutdownAsync += (_, _) =>
        {
            inbox.Writer.TryComplete();
            return Task.CompletedTask;
        };

        await channel.BasicConsumeAsync(queue, false, consumer, cancellationToken);

        await foreach (var delivery in inbox.Reader.ReadAllAsync(cancellationToken))
        {
            await using var scope = scopes.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<NotificationEventDelivery<T>>();
            var ack = await handler.ShouldAcknowledgeAsync(
                delivery.Body,
                delivery.Key,
                delivery.Type,
                delivery.Id,
                delivery.Tenant,
                reason => logger.LogWarning("{Queue} delivery: {Reason}", queue, reason),
                cancellationToken);

            if (ack)
                await channel.BasicAckAsync(delivery.Tag, false, cancellationToken);
            else
                await channel.BasicRejectAsync(delivery.Tag, false, cancellationToken);
        }
    }

    private sealed record Delivery(ulong Tag, byte[] Body, string Key, string? Type, string? Id, string? Tenant);
}
