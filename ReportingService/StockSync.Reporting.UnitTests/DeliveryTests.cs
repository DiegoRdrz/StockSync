using StockSync.Reporting.Application;

namespace StockSync.Reporting.UnitTests;

public sealed class DeliveryTests
{
    private sealed class Writer(int failures, ProjectionResult result) : ISalesProjection
    {
        public int Calls { get; private set; }
        public Task<ProjectionResult> ApplyAsync(SaleConfirmed sale, CancellationToken token)
        {
            if (++Calls <= failures) throw new TimeoutException();
            return Task.FromResult(result);
        }
    }

    [Theory]
    [InlineData(0, ProjectionResult.Applied, true, 1)]
    [InlineData(0, ProjectionResult.Duplicate, true, 1)]
    [InlineData(2, ProjectionResult.Applied, true, 3)]
    [InlineData(3, ProjectionResult.Applied, false, 3)]
    public async Task SettlementDependsOnProjectionResult(int failures, ProjectionResult result, bool ack, int attempts)
    {
        var sale = SalesContractTests.Parse();
        var writer = new Writer(failures, result);
        var handler = new SalesDelivery(writer, TimeProvider.System);
        Assert.Equal(ack, await handler.ShouldAcknowledgeAsync(SalesContractTests.Fixture, SaleConfirmed.RoutingKey,
            SaleConfirmed.RoutingKey, sale.EventId.ToString(), sale.TenantId.ToString(), _ => { }, default));
        Assert.Equal(attempts, writer.Calls);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("missing tenant")]
    [InlineData("routing")]
    [InlineData("type")]
    [InlineData("json")]
    [InlineData("fields")]
    public async Task InvalidDeliveryIsRejectedBeforeWriting(string invalid)
    {
        var sale = SalesContractTests.Parse();
        var writer = new Writer(0, ProjectionResult.Applied);
        var bytes = invalid switch { "json" => "{"u8.ToArray(), "fields" => "{}"u8.ToArray(), _ => SalesContractTests.Fixture };
        var tenant = invalid switch { "tenant" => Guid.NewGuid().ToString(), "missing tenant" => null, _ => sale.TenantId.ToString() };
        var ack = await new SalesDelivery(writer, TimeProvider.System).ShouldAcknowledgeAsync(bytes,
            invalid == "routing" ? "other" : SaleConfirmed.RoutingKey, invalid == "type" ? null : SaleConfirmed.RoutingKey,
            sale.EventId.ToString(), tenant, _ => { }, default);
        Assert.False(ack);
        Assert.Equal(0, writer.Calls);
    }
}
