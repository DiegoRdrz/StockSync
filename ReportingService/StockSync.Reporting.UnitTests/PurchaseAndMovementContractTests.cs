using System.Text.Json;
using StockSync.Reporting.Application;
using StockSync.Reporting.Domain;

namespace StockSync.Reporting.UnitTests;

public class PurchaseAndMovementContractTests
{
    internal static byte[] Fixture(string name) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Contracts", name + ".v1.json"));

    internal static T Parse<T>(string name) where T : class, IReportingEvent<T>
    {
        var bytes = Fixture(name);
        using var json = JsonDocument.Parse(bytes);
        return ReportingEvent.Parse<T>(bytes, T.RoutingKey, T.RoutingKey,
            json.RootElement.GetProperty("eventId").GetString(), json.RootElement.GetProperty("tenantId").GetString());
    }

    [Fact]
    public void CanonicalPurchasePreservesTheSupplierAmountsAndReceiptMovement()
    {
        var message = Parse<PurchaseOrderReceived>("purchase-order-received");
        var row = message.Project();
        Assert.Equal(Guid.Parse("ee600d51-b6d0-4604-a67b-541c07ebccc5"), row.PurchaseOrderId);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), row.BranchId);
        Assert.Equal("North Ridge Tools", row.SupplierName);
        Assert.Equal(new DateTime(2026, 11, 18, 12, 15, 0, 155, DateTimeKind.Utc), row.ReceivedAt);
        Assert.Equal(80m, row.Total);
        Assert.Equal(10, row.Units);
        var line = Assert.Single(row.Lines);
        Assert.Equal(row.PurchaseOrderId, line.PurchaseOrderId);
        Assert.Equal(1, line.LineNumber);
        Assert.Equal(Guid.Parse("acb7143d-f88c-4d2d-9526-5b71f5e9db0c"), line.ProductId);
        Assert.Equal(("DEMO-HER-001", "Demo hammer", 10, 8m, 80m),
            (line.ProductSku, line.ProductName, line.Quantity, line.UnitPrice, line.LineTotal));
        Assert.Equal(Guid.Parse("9bebde81-1725-41d8-b571-cd48475fedcf"), line.StockMovementId);
    }

    [Fact]
    public void CanonicalIssuePreservesTheMovementAndProjectsTheResultingLevel()
    {
        var message = Parse<StockMovementRecorded>("stock-movement-recorded");
        var date = new DateTime(2026, 11, 18, 14, 30, 0, 120, DateTimeKind.Utc);
        var expected = new StockMovement(Guid.Parse("a47322d6-73b2-49f5-ac59-8fdc7ce0f445"),
            Guid.Parse("f53e4aab-5c92-4300-9b57-c6f4157cd878"), Guid.Parse("acb7143d-f88c-4d2d-9526-5b71f5e9db0c"),
            Guid.Parse("11111111-1111-1111-1111-111111111111"), "Issue", 3, 10, 7, date, "DEMO-HER-001",
            "Demo hammer", 5, Guid.Parse("8ffc6a82-a9ba-46b3-b33c-abbad218c44d"));
        Assert.Equal(expected, message.ProjectMovement());
        Assert.Equal(new StockLevel(expected.StockId, expected.BranchId, expected.ProductId, expected.ProductSku,
            expected.ProductName, 7, 5, date, expected.MovementId), message.ProjectLevel());
    }
}
