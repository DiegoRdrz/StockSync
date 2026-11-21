using System.Text.Json;
using StockSync.Notification.Application.IntegrationEvents;

namespace StockSync.Notification.UnitTests.Application;

public class ContractAndDeliveryTests
{
    private static byte[] Fixture(string name) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Contracts", name + ".v1.json"));

    private static T Parse<T>(string name) where T : class, INotificationEvent<T>
    {
        var bytes = Fixture(name);
        using var json = JsonDocument.Parse(bytes);
        return NotificationEventParser.Parse<T>(
            bytes,
            T.RoutingKey,
            T.RoutingKey,
            json.RootElement.GetProperty("eventId").GetString(),
            json.RootElement.GetProperty("tenantId").GetString());
    }

    [Fact]
    public void CanonicalOrderCreatedContract_ParsesCleanly()
    {
        var order = Parse<OrderCreatedEvent>("order-created");

        Assert.Equal(Guid.Parse("3f9a15c4-41d1-4a7b-92c0-7478b1b1d890"), order.EventId);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000001"), order.TenantId);
        Assert.Equal(Guid.Parse("028abc5d-689a-4f2f-8664-f384d25bfe53"), order.OrderId);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), order.BranchId);
    }

    [Fact]
    public void CanonicalStockMovementContract_ParsesAndEvaluatesLowStockThreshold()
    {
        var movement = Parse<StockMovementRecordedEvent>("stock-movement-recorded");

        Assert.Equal(Guid.Parse("a47322d6-73b2-49f5-ac59-8fdc7ce0f445"), movement.MovementId);
        Assert.Equal("DEMO-HER-001", movement.ProductSku);
        Assert.Equal(7, movement.NewQuantity);
        Assert.Equal(5, movement.MinimumStock);
        Assert.False(movement.TriggersLowStockAlert);

        var belowThreshold = movement with { PreviousQuantity = 7, NewQuantity = 3 };
        Assert.True(belowThreshold.TriggersLowStockAlert);
        Assert.Equal(2, belowThreshold.Shortage);
    }

    [Theory]
    [InlineData(0, EventProcessingOutcome.Applied, true, 1)]
    [InlineData(0, EventProcessingOutcome.Duplicate, true, 1)]
    [InlineData(2, EventProcessingOutcome.Applied, true, 3)]
    [InlineData(3, EventProcessingOutcome.Applied, false, 3)]
    public async Task DeliverySettlement_RetriesTransientErrorsUpToThreeAttempts(
        int failures,
        EventProcessingOutcome outcome,
        bool expectedAck,
        int expectedCalls)
    {
        var bytes = Fixture("order-created");
        var order = Parse<OrderCreatedEvent>("order-created");
        var processor = new FakeProcessor(failures, outcome);
        var delivery = new NotificationEventDelivery<OrderCreatedEvent>(processor, TimeProvider.System);

        var ack = await delivery.ShouldAcknowledgeAsync(
            bytes,
            OrderCreatedEvent.RoutingKey,
            OrderCreatedEvent.RoutingKey,
            order.EventId.ToString(),
            order.TenantId.ToString(),
            _ => { },
            CancellationToken.None);

        Assert.Equal(expectedAck, ack);
        Assert.Equal(expectedCalls, processor.Calls);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("missing-tenant")]
    [InlineData("routing")]
    [InlineData("type")]
    [InlineData("json")]
    [InlineData("fields")]
    public async Task InvalidDelivery_IsRejectedWithoutInvokingProcessor(string invalidCase)
    {
        var fixture = Fixture("order-created");
        var order = Parse<OrderCreatedEvent>("order-created");
        var processor = new FakeProcessor(0, EventProcessingOutcome.Applied);
        var bytes = invalidCase switch
        {
            "json" => "{"u8.ToArray(),
            "fields" => "{}"u8.ToArray(),
            _ => fixture
        };
        var tenant = invalidCase switch
        {
            "tenant" => Guid.NewGuid().ToString(),
            "missing-tenant" => null,
            _ => order.TenantId.ToString()
        };

        var ack = await new NotificationEventDelivery<OrderCreatedEvent>(processor, TimeProvider.System)
            .ShouldAcknowledgeAsync(
                bytes,
                invalidCase == "routing" ? "other.key" : OrderCreatedEvent.RoutingKey,
                invalidCase == "type" ? null : OrderCreatedEvent.RoutingKey,
                order.EventId.ToString(),
                tenant,
                _ => { },
                CancellationToken.None);

        Assert.False(ack);
        Assert.Equal(0, processor.Calls);
    }

    private sealed class FakeProcessor(int failures, EventProcessingOutcome outcome)
        : IEventProcessor<OrderCreatedEvent>
    {
        public int Calls { get; private set; }

        public Task<EventProcessingOutcome> ProcessAsync(OrderCreatedEvent message, CancellationToken cancellationToken)
        {
            if (++Calls <= failures)
                throw new TimeoutException();

            return Task.FromResult(outcome);
        }
    }
}
