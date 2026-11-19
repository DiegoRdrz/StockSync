using Microsoft.EntityFrameworkCore;
using StockSync.Reporting.Application;
using StockSync.Reporting.Application.Common;
using StockSync.Reporting.Infrastructure;

namespace StockSync.Reporting.IntegrationTests;

public class SalesPostgresTests : PostgresTestBase
{
    private static SaleConfirmed Sample(Guid tenant, Guid branch, DateTime date, Guid product) =>
        new(Guid.NewGuid(), date, tenant, Guid.NewGuid(), branch, date, 24,
            [new(1, product, "CODE", "Hammer", 2, 12, 24, Guid.NewGuid())]);

    private async Task<ProjectionResult> Apply(SaleConfirmed sale)
    {
        var tenant = new TenantContext();
        await using var db = new ReportingDbContext(Options(), tenant);
        return await new SalesProjection(db, tenant).ApplyAsync(sale, default);
    }

    [PostgreSqlFact]
    public async Task RankingUsesQuantityRevenueThenProductIdAcrossMultilineSales()
    {
        var cheap = Guid.Parse("00000000-0000-4000-8000-000000000001");
        var second = Guid.Parse("00000000-0000-4000-8000-000000000002");
        var third = Guid.Parse("00000000-0000-4000-8000-000000000003");
        var tenant = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var sale = Sample(tenant, Guid.NewGuid(), date, cheap) with
        {
            Total = 47,
            Lines = [
                new(1, third, "C", "Third", 2, 10, 20, Guid.NewGuid()),
                new(2, second, "B", "Second", 2, 10, 20, Guid.NewGuid()),
                new(3, cheap, "A", "First", 7, 1, 7, Guid.NewGuid())
            ]
        };
        await Apply(sale);
        var fourth = Guid.NewGuid();
        await Apply(sale with
        {
            EventId = Guid.NewGuid(), OrderId = Guid.NewGuid(), Total = 60,
            Lines = [new(1, fourth, "D", "Fourth", 2, 30, 60, Guid.NewGuid())]
        });
        await using var db = CreateContext(tenant);
        var reports = new SalesReports(db);
        var filter = new SalesFilter(date, date, null);
        Assert.Equal(new SalesSummary(107, 2, 53.5m, 13), await reports.SummaryAsync(filter, default));
        var ranked = await reports.TopAsync(filter, 3, default);
        Assert.Equal(new[] { cheap, fourth, second }, ranked.Select(x => x.ProductId));
    }

    [PostgreSqlFact]
    public async Task MigrationAndConcurrentRedeliveryProduceOneSale()
    {
        var sale = Sample(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid());
        var results = await Task.WhenAll(Apply(sale), Apply(sale));
        Assert.Contains(ProjectionResult.Applied, results);
        Assert.Contains(ProjectionResult.Duplicate, results);
        await using var db = CreateContext(sale.TenantId);
        Assert.Equal(new[] { "20261119000000_InitialReporting", "20261119234545_PurchasesAndInventory" },
            await db.Database.GetAppliedMigrationsAsync());
        Assert.Single(await db.ProcessedEvents.ToListAsync());
        Assert.Single(await db.Sales.ToListAsync());
        Assert.Single(await db.SaleLines.ToListAsync());
    }

    [PostgreSqlFact]
    public async Task ProjectionConflictRollsBackProcessedEvent()
    {
        var sale = Sample(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid());
        await Apply(sale);
        await Assert.ThrowsAsync<DbUpdateException>(() => Apply(sale with { EventId = Guid.NewGuid() }));
        await using var db = CreateContext(sale.TenantId);
        Assert.Equal(sale.EventId, (await db.ProcessedEvents.SingleAsync()).EventId);
    }

    [PostgreSqlFact]
    public async Task QueriesRespectTenantBranchInclusiveDatesAndStablePagination()
    {
        var tenant = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var date = new DateTime(2026, 10, 15, 23, 55, 0, DateTimeKind.Utc);
        var product = Guid.NewGuid();
        var start = Sample(tenant, branch, date, product);
        var end = Sample(tenant, branch, date.AddMinutes(10), product);
        await Apply(start);
        await Apply(end);
        await Apply(Sample(tenant, branch, date.AddSeconds(-1), product));
        await Apply(Sample(tenant, Guid.NewGuid(), date, product));
        await Apply(start with { EventId = Guid.NewGuid(), TenantId = Guid.NewGuid() });
        await using var db = CreateContext(tenant);
        var reports = new SalesReports(db);
        var filter = new SalesFilter(date, end.ConfirmedAt, branch);
        Assert.Equal(new SalesSummary(48, 2, 24, 4), await reports.SummaryAsync(filter, default));
        var daily = await reports.DailyAsync(filter, default);
        Assert.Equal(new[] { date.Date, end.ConfirmedAt.Date }, daily.Select(d => d.Day));
        Assert.All(daily, d => { Assert.Equal(24, d.Amount); Assert.Equal(1, d.Count); Assert.Equal(2, d.Units); });
        Assert.Equal(new ProductSales(product, "CODE", "Hammer", 4, 48), Assert.Single(await reports.TopAsync(filter, 1, default)));
        var first = await reports.DetailAsync(filter, 1, 1, default);
        Assert.Equal(2, first.TotalPages);
        Assert.Equal(end.OrderId, Assert.Single(first.Items).OrderId);
        var next = Assert.Single((await reports.DetailAsync(filter, 2, 1, default)).Items);
        Assert.Equal(start.OrderId, next.OrderId);
        Assert.Equal(product, Assert.Single(next.Lines).ProductId);
        Assert.Equal(new SalesSummary(0, 0, 0, 0), await reports.SummaryAsync(filter with { BranchId = Guid.NewGuid() }, default));
    }
}
