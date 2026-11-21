using System.Text.Json;
using System.Text.Json.Nodes;
using StockSync.Reporting.Application;

namespace StockSync.Reporting.UnitTests;

public class PurchaseAndMovementDeliveryTests
{
    private sealed class Duplicate<T> : IProjection<T>
    {
        public int Calls { get; private set; }
        public Task<ProjectionResult> ApplyAsync(T message, CancellationToken token)
        {
            Calls++;
            return Task.FromResult(ProjectionResult.Duplicate);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PurchaseRedeliveryRequiresMatchingTenant(bool validTenant) =>
        await CheckDuplicate<PurchaseOrderReceived>("purchase-order-received", validTenant);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MovementRedeliveryRequiresMatchingTenant(bool validTenant) =>
        await CheckDuplicate<StockMovementRecorded>("stock-movement-recorded", validTenant);

    private static async Task CheckDuplicate<T>(string fixture, bool validTenant) where T : class, IReportingEvent<T>
    {
        var message = PurchaseAndMovementContractTests.Parse<T>(fixture);
        var writer = new Duplicate<T>();
        var ack = await new EventDelivery<T>(writer, TimeProvider.System).ShouldAcknowledgeAsync(
            PurchaseAndMovementContractTests.Fixture(fixture), T.RoutingKey, T.RoutingKey, message.EventId.ToString(),
            validTenant ? message.TenantId.ToString() : null, _ => { }, default);
        Assert.Equal(validTenant, ack);
        Assert.Equal(validTenant ? 1 : 0, writer.Calls);
    }

    [Theory]
    [InlineData("newQuantity")]
    [InlineData("minimumStock")]
    [InlineData("categoryId")]
    [InlineData("movementId")]
    [InlineData("occurredAt")]
    public async Task InvalidMovementIsRejectedBeforeProjection(string field)
    {
        var json = JsonNode.Parse(PurchaseAndMovementContractTests.Fixture("stock-movement-recorded"))!.AsObject();
        if (field == "movementId") json[field] = Guid.NewGuid();
        else if (field == "occurredAt") json[field] = DateTime.UnixEpoch;
        else json.Remove(field);
        var writer = new Duplicate<StockMovementRecorded>();
        var ack = await new EventDelivery<StockMovementRecorded>(writer, TimeProvider.System).ShouldAcknowledgeAsync(
            JsonSerializer.SerializeToUtf8Bytes(json), StockMovementRecorded.RoutingKey, StockMovementRecorded.RoutingKey,
            (string?)json["eventId"], (string?)json["tenantId"], _ => { }, default);
        Assert.False(ack);
        Assert.Equal(0, writer.Calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ProjectedOrderingUsesSourceTimeThenMovementIdEvenWhenLatestArrivesFirst(int secondsApart)
    {
        var first = PurchaseAndMovementContractTests.Parse<StockMovementRecorded>("stock-movement-recorded");
        var laterId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var later = first with { EventId = laterId, MovementId = laterId,
            MovementDate = first.MovementDate.AddSeconds(secondsApart), OccurredAt = first.OccurredAt.AddSeconds(secondsApart) };
        var latest = later.ProjectLevel();
        var older = first.ProjectLevel();
        Assert.True((latest.LastOccurredAt, latest.LastMovementId).CompareTo((older.LastOccurredAt, older.LastMovementId)) > 0);
        Assert.Equal((first.MovementDate, first.EventId), (older.LastOccurredAt, older.LastMovementId));
    }
}
