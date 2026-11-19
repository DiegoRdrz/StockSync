using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StockSync.Reporting.Application;

namespace StockSync.Reporting.Infrastructure;

public sealed class ReportingConsumer<T>(Uri broker, IServiceScopeFactory scopes, ILogger<ReportingConsumer<T>> logger) : BackgroundService
    where T : class, IReportingEvent<T>
{
    public const string Exchange = "stocksync.events";
    public const string DeadLetterExchange = "reporting.dlx";

    public static async Task DeclareAsync(IChannel channel, string queue, CancellationToken token, string exchange = Exchange)
    {
        await channel.ExchangeDeclareAsync(exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: token);
        await channel.ExchangeDeclareAsync(DeadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: token);
        var error = queue + ".error";
        await channel.QueueDeclareAsync(error, durable: true, exclusive: false, autoDelete: false, cancellationToken: token);
        await channel.QueueBindAsync(error, DeadLetterExchange, error, cancellationToken: token);
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = DeadLetterExchange, ["x-dead-letter-routing-key"] = error },
            cancellationToken: token);
        await channel.QueueBindAsync(queue, exchange, T.RoutingKey, cancellationToken: token);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory { Uri = broker, AutomaticRecoveryEnabled = true, ConsumerDispatchConcurrency = 1 };
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await factory.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                await ConsumeAsync(channel, T.ConsumerName, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                logger.LogWarning("Subscription {Queue} interrupted ({Reason}).", T.ConsumerName, ex.GetType().Name);
            }
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }

    public async Task ConsumeAsync(IChannel channel, string queue, CancellationToken token, string exchange = Exchange)
    {
        await DeclareAsync(channel, queue, token, exchange);
        await channel.BasicQosAsync(0, 1, false, token);
        // A single reader owns settlement and shutdown. Copy the body before the callback returns.
        var inbox = System.Threading.Channels.Channel.CreateUnbounded<Delivery>();
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, args) =>
        {
            object? header = null;
            args.BasicProperties.Headers?.TryGetValue("tenant-id", out header);
            var tenant = header is byte[] bytes ? Encoding.UTF8.GetString(bytes) : header as string;
            inbox.Writer.TryWrite(new(args.DeliveryTag, args.Body.ToArray(), args.RoutingKey,
                args.BasicProperties.Type, args.BasicProperties.MessageId, tenant));
            return Task.CompletedTask;
        };
        consumer.UnregisteredAsync += (_, _) => { inbox.Writer.TryComplete(); return Task.CompletedTask; };
        channel.ChannelShutdownAsync += (_, _) => { inbox.Writer.TryComplete(); return Task.CompletedTask; };
        await channel.BasicConsumeAsync(queue, false, consumer, token);
        await foreach (var delivery in inbox.Reader.ReadAllAsync(token))
        {
            await using var scope = scopes.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<EventDelivery<T>>();
            var ack = await handler.ShouldAcknowledgeAsync(delivery.Body, delivery.Key, delivery.Type, delivery.Id,
                delivery.Tenant, reason => logger.LogWarning("{Queue} delivery: {Reason}", queue, reason), token);
            if (ack)
                await channel.BasicAckAsync(delivery.Tag, false, token);
            else
                await channel.BasicRejectAsync(delivery.Tag, false, token);
        }
    }

    private sealed record Delivery(ulong Tag, byte[] Body, string Key, string? Type, string? Id, string? Tenant);
}
