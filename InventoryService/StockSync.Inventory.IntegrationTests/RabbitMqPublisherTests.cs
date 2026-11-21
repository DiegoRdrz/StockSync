using System.Text;
using RabbitMQ.Client;
using StockSync.Inventory.Infrastructure.Messaging;
using StockSync.Inventory.Infrastructure.Outbox;

namespace StockSync.Inventory.IntegrationTests;

public sealed class RabbitMqFactAttribute : FactAttribute
{
    public RabbitMqFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STOCKSYNC_TEST_RABBITMQ")))
            Skip = "Set STOCKSYNC_TEST_RABBITMQ to run against RabbitMQ.";
    }
}

public sealed class RabbitMqPublisherTests
{
    [RabbitMqFact]
    public async Task Publisher_RoutesConfirmedPersistentJsonWithEventAndTenantMetadata()
    {
        var uri = Environment.GetEnvironmentVariable("STOCKSYNC_TEST_RABBITMQ")!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var token = timeout.Token;
        var exchange = $"stocksync.inventory.test.{Guid.NewGuid():N}";
        var fixture = await StockMovementRecordedContractTests.ReadFixtureAsync();
        var movement = StockMovementRecordedContractTests.DeserializeFixture(fixture);
        var row = OutboxMessage.From(movement);

        var factory = new ConnectionFactory { Uri = new Uri(uri) };
        await using var connection = await factory.CreateConnectionAsync(token);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: token);
        await channel.ExchangeDeclareAsync(exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: token);
        try
        {
            var queue = await channel.QueueDeclareAsync("", durable: false, exclusive: true, autoDelete: true,
                cancellationToken: token);
            await channel.QueueBindAsync(queue.QueueName, exchange, "inventory.*.v1", cancellationToken: token);

            await using var publisher = new RabbitMqPublisher(new RabbitMqOptions(uri, exchange));
            await publisher.PublishAsync(row, token);

            BasicGetResult? message;
            while ((message = await channel.BasicGetAsync(queue.QueueName, autoAck: true, cancellationToken: token)) is null)
                await Task.Delay(50, token);

            Assert.Equal(exchange, message.Exchange);
            Assert.Equal("inventory.stock-movement-recorded.v1", message.RoutingKey);
            Assert.Equal(row.EventType, message.BasicProperties.Type);
            Assert.Equal(movement.EventId.ToString(), message.BasicProperties.MessageId);
            Assert.Equal("application/json", message.BasicProperties.ContentType);
            Assert.Equal(DeliveryModes.Persistent, message.BasicProperties.DeliveryMode);
            Assert.Equal(new DateTimeOffset(movement.MovementDate).ToUnixTimeSeconds(),
                message.BasicProperties.Timestamp.UnixTime);
            var tenant = Assert.IsType<byte[]>(message.BasicProperties.Headers!["tenant-id"]);
            Assert.Equal(movement.TenantId.ToString(), Encoding.UTF8.GetString(tenant));
            Assert.Equal(row.Payload, Encoding.UTF8.GetString(message.Body.Span));
        }
        finally
        {
            await channel.ExchangeDeleteAsync(exchange);
        }
    }
}
