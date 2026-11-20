using System.Text.Json;
using System.Text.Json.Nodes;
using StockSync.Inventory.Application.IntegrationEvents;
using StockSync.Inventory.Infrastructure.Outbox;

namespace StockSync.Inventory.IntegrationTests;

public sealed class StockMovementRecordedContractTests
{
    [Fact]
    public async Task V1Fixture_MatchesSerializedOutboxPayload()
    {
        var fixture = await ReadFixtureAsync();
        var movement = DeserializeFixture(fixture);
        Assert.Equal(Guid.Parse("a47322d6-73b2-49f5-ac59-8fdc7ce0f445"), movement.EventId);
        Assert.Equal(movement.EventId, movement.MovementId);
        Assert.Equal(new DateTime(2026, 11, 18, 14, 30, 0, 120, DateTimeKind.Utc), movement.OccurredAt);
        Assert.Equal(movement.OccurredAt, movement.MovementDate);
        Assert.Equal("Issue", movement.Type);
        Assert.Equal((3, 10, 7), (movement.Quantity, movement.PreviousQuantity, movement.NewQuantity));
        Assert.Equal(("DEMO-HER-001", "Demo hammer", 5),
            (movement.ProductSku, movement.ProductName, movement.MinimumStock));
        Assert.Equal(Guid.Parse("8ffc6a82-a9ba-46b3-b33c-abbad218c44d"), movement.CategoryId);

        var row = OutboxMessage.From(movement);
        Assert.Equal("inventory.stock-movement-recorded.v1", row.EventType);
        Assert.Equal(movement.TenantId, row.TenantId);
        Assert.Equal(movement.EventId, row.Id);
        var expected = JsonNode.Parse(fixture)!.AsObject();
        var actual = JsonNode.Parse(row.Payload)!.AsObject();

        // Trailing fractional zeros can differ between the fixture and System.Text.Json.
        foreach (var name in new[] { "occurredAt", "movementDate" })
        {
            var date = actual[name]!.GetValue<DateTime>();
            Assert.Equal(DateTimeKind.Utc, date.Kind);
            Assert.Equal(expected[name]!.GetValue<DateTime>(), date);
            expected.Remove(name);
            actual.Remove(name);
        }
        Assert.True(JsonNode.DeepEquals(expected, actual),
            "The payload must match the fixture's field names, values and JSON types.");
    }

    internal static Task<string> ReadFixtureAsync() =>
        File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Contracts", "stock-movement-recorded.v1.json"));

    internal static StockMovementRecorded DeserializeFixture(string fixture) =>
        Assert.IsType<StockMovementRecorded>(JsonSerializer.Deserialize<StockMovementRecorded>(
            fixture, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
}
