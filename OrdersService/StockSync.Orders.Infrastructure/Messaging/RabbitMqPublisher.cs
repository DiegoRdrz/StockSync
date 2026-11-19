using System.Text;
using RabbitMQ.Client;
using StockSync.Orders.Infrastructure.Outbox;

namespace StockSync.Orders.Infrastructure.Messaging;

public sealed record RabbitMqOptions(string ConnectionString, string Exchange);

// Publishes to a durable topic exchange; the routing key is the event type
// (for example orders.sale-confirmed.v1). Consumers declare and bind their own queues;
// messages published while no queue is bound are discarded by RabbitMQ.
public sealed class RabbitMqPublisher : IMessagePublisher, IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqPublisher(RabbitMqOptions options)
    {
        _options = options;
    }

    public async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var channel = await GetChannelAsync(cancellationToken);
            var properties = new BasicProperties
            {
                MessageId = message.Id.ToString(),
                Type = message.EventType,
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                Timestamp = new AmqpTimestamp(new DateTimeOffset(message.OccurredAt, TimeSpan.Zero).ToUnixTimeSeconds()),
                Headers = new Dictionary<string, object?> { ["tenant-id"] = message.TenantId.ToString() }
            };

            // With publisher confirmation tracking, this waits for the broker acknowledgement.
            await channel.BasicPublishAsync(
                _options.Exchange,
                message.EventType,
                mandatory: false,
                properties,
                Encoding.UTF8.GetBytes(message.Payload),
                cancellationToken);
        }
        catch
        {
            // Reconnect on the next attempt.
            await CloseAsync();
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
            return _channel;

        await CloseAsync();
        var factory = new ConnectionFactory
        {
            Uri = new Uri(_options.ConnectionString),
            ClientProvidedName = "stocksync-orders"
        };
        _connection = await factory.CreateConnectionAsync(cancellationToken);
        _channel = await _connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);
        await _channel.ExchangeDeclareAsync(
            _options.Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        return _channel;
    }

    private async Task CloseAsync()
    {
        if (_channel is not null)
            await _channel.DisposeAsync();
        if (_connection is not null)
            await _connection.DisposeAsync();
        _channel = null;
        _connection = null;
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
        _lock.Dispose();
    }
}
