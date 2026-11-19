using System.Text.Json;
using StockSync.Reporting.Application;

namespace StockSync.Reporting.UnitTests;

public class SalesContractTests
{
    internal static byte[] Fixture => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Contracts", "sale-confirmed.v1.json"));

    internal static SaleConfirmed Parse()
    {
        using var json = JsonDocument.Parse(Fixture);
        var body = json.RootElement;
        return ReportingEvent.Parse<SaleConfirmed>(Fixture, SaleConfirmed.RoutingKey, SaleConfirmed.RoutingKey,
            body.GetProperty("eventId").GetString(), body.GetProperty("tenantId").GetString());
    }

    [Fact]
    public void CanonicalSaleProjectsItsOriginalHeaderAndLineValues()
    {
        var sale = Parse();
        var row = sale.Project();
        Assert.Equal(sale.OrderId, row.OrderId);
        Assert.Equal(sale.BranchId, row.BranchId);
        Assert.Equal(sale.ConfirmedAt, row.ConfirmedAt);
        Assert.Equal(37.5m, row.Total);
        Assert.Equal(3, row.Units);
        var line = Assert.Single(row.Lines);
        Assert.Equal("DEMO-HER-001", line.ProductSku);
        Assert.Equal("Demo hammer", line.ProductName);
        Assert.Equal(sale.Lines[0].ProductId, line.ProductId);
        Assert.Equal(12.5m, line.UnitPrice);
        Assert.Equal(row.Total, line.LineTotal);
    }
}
