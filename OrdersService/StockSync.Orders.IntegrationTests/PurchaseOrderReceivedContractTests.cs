using System.Text.Json;
using System.Text.Json.Nodes;
using StockSync.Orders.Application.IntegrationEvents;
using StockSync.Orders.Infrastructure.Outbox;

namespace StockSync.Orders.IntegrationTests;

public sealed class PurchaseOrderReceivedContractTests
{
    [Fact]
    public async Task V1Fixture_DeserializesAndMatchesOutboxJson()
    {
        var fixture = await File.ReadAllTextAsync(Path.Combine(
            AppContext.BaseDirectory, "Contracts", "purchase-order-received.v1.json"));
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var received = Assert.IsType<PurchaseOrderReceived>(JsonSerializer.Deserialize<PurchaseOrderReceived>(fixture, options));

        Assert.Equal(Guid.Parse("de172114-e149-49f7-8e45-b84e59ccfc24"), received.EventId);
        Assert.Equal(new DateTime(2026, 11, 18, 12, 15, 0, 159, DateTimeKind.Utc), received.OccurredAt);
        Assert.Equal(DateTimeKind.Utc, received.OccurredAt.Kind);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000001"), received.TenantId);
        Assert.Equal(Guid.Parse("ee600d51-b6d0-4604-a67b-541c07ebccc5"), received.PurchaseOrderId);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), received.BranchId);
        Assert.Equal("North Ridge Tools", received.SupplierName);
        Assert.Equal(new DateTime(2026, 11, 18, 12, 15, 0, 155, DateTimeKind.Utc), received.ReceivedAt);
        Assert.Equal(DateTimeKind.Utc, received.ReceivedAt.Kind);
        Assert.Equal(80m, received.Total);
        Assert.Equal(new PurchaseOrderReceivedLine(
            1, Guid.Parse("acb7143d-f88c-4d2d-9526-5b71f5e9db0c"), "DEMO-HER-001", "Demo hammer",
            10, 8m, 80m, Guid.Parse("9bebde81-1725-41d8-b571-cd48475fedcf")), Assert.Single(received.Lines));

        var message = OutboxMessage.From(received);
        Assert.Equal("orders.purchase-order-received.v1", message.EventType);
        Assert.Equal(received.EventId, message.Id);
        Assert.Equal(received.TenantId, message.TenantId);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(fixture), JsonNode.Parse(message.Payload)),
            "Outbox JSON must match the v1 fixture, including field names, numeric tokens and nested lines.");
    }
}
