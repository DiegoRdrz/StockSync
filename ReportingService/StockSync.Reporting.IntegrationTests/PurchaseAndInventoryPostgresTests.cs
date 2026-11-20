using Microsoft.EntityFrameworkCore;
using StockSync.Reporting.Application;
using StockSync.Reporting.Application.Common;
using StockSync.Reporting.Infrastructure;

namespace StockSync.Reporting.IntegrationTests;

public class PurchaseAndInventoryPostgresTests : PostgresTestBase
{
    private static readonly DateTime Day = new(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    private static PurchaseOrderReceived Purchase(Guid tenant, Guid branch, DateTime date) =>
        new(Guid.NewGuid(), date, tenant, Guid.NewGuid(), branch, "Supplier", date, 31.5m,
            [new(1, Guid.NewGuid(), "A", "Hammer", 2, 10, 20, Guid.NewGuid()),
             new(2, Guid.NewGuid(), "B", "Gloves", 1, 11.5m, 11.5m, Guid.NewGuid())]);

    private static StockMovementRecorded Movement(Guid tenant, Guid stock, Guid branch, Guid product,
        DateTime time, int previous, int balance, Guid? id = null)
    {
        var movement = id ?? Guid.NewGuid();
        return new(movement, time, tenant, movement, stock, product, branch, balance > previous ? "Receipt" : "Issue",
            Math.Abs(balance - previous), previous, balance, time, "A", "Hammer", 10, null);
    }

    private async Task<ProjectionResult> Apply(PurchaseOrderReceived message)
    {
        var tenant = new TenantContext();
        await using var db = new ReportingDbContext(Options(), tenant);
        return await new PurchaseProjection(db, tenant).ApplyAsync(message, default);
    }

    private async Task<ProjectionResult> Apply(StockMovementRecorded message)
    {
        var tenant = new TenantContext();
        await using var db = new ReportingDbContext(Options(), tenant);
        return await new StockMovementProjection(db, tenant).ApplyAsync(message, default);
    }

    [PostgreSqlFact]
    public async Task ConcurrentPurchaseRedeliveryIsAtomicAndBusinessKeyConflictsAreNotDuplicates()
    {
        var purchase = Purchase(Guid.NewGuid(), Guid.NewGuid(), Day);
        var results = await Task.WhenAll(Apply(purchase), Apply(purchase));
        Assert.Contains(ProjectionResult.Applied, results);
        Assert.Contains(ProjectionResult.Duplicate, results);
        await Assert.ThrowsAsync<DbUpdateException>(() => Apply(purchase with { EventId = Guid.NewGuid() }));
        await using var db = CreateContext(purchase.TenantId);
        Assert.Equal(purchase.EventId, (await db.ProcessedEvents.SingleAsync()).EventId);
        var row = await db.Purchases.Include(p => p.Lines).SingleAsync();
        Assert.Equal("Supplier", row.SupplierName);
        Assert.Equal(purchase.Lines.Select(l => l.StockMovementId), row.Lines.OrderBy(l => l.LineNumber).Select(l => l.StockMovementId));
        Assert.Empty(await db.StockLevels.ToListAsync());
    }

    [PostgreSqlFact]
    public async Task PurchaseReportsUseReceivedTimeInclusiveBoundariesBranchAndTenant()
    {
        var tenant = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var first = Purchase(tenant, branch, Day);
        await Apply(first with { OccurredAt = Day.AddDays(-1) });
        await Apply(Purchase(tenant, branch, Day.AddDays(1)));
        await Apply(Purchase(tenant, branch, Day));
        await Apply(Purchase(tenant, branch, Day.AddTicks(-10)));
        await Apply(Purchase(tenant, Guid.NewGuid(), Day));
        await Apply(first with { TenantId = Guid.NewGuid(), EventId = Guid.NewGuid() });
        await using var db = CreateContext(tenant);
        var reports = new PurchaseReports(db);
        var filter = new SalesFilter(Day, Day.AddDays(1), branch);
        Assert.Equal(new PurchaseSummary(94.5m, 3, 9), await reports.SummaryAsync(filter, default));
        Assert.Equal(new[] { new DailyPurchases(Day, 63, 2, 6), new DailyPurchases(Day.AddDays(1), 31.5m, 1, 3) },
            await reports.DailyAsync(filter, default));
        Assert.Equal(new PurchaseSummary(0, 0, 0), await reports.SummaryAsync(filter with { BranchId = Guid.NewGuid() }, default));
        Assert.Equal(10, await db.PurchaseLines.CountAsync());
    }

    [PostgreSqlFact]
    public async Task ConcurrentMovementsAndRedeliveryKeepAllHistoryAndTheGreatestTuple()
    {
        var tenant = Guid.NewGuid();
        var stock = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var product = Guid.NewGuid();
        var early = Movement(tenant, stock, branch, product, Day, 0, 10, Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"));
        var lower = Movement(tenant, stock, branch, product, Day.AddDays(1), 10, 8,
            Guid.Parse("00000001-0000-0000-0000-000000000000"));
        var winner = Movement(tenant, stock, branch, product, lower.MovementDate, 8, 6,
            Guid.Parse("01000000-0000-0000-0000-000000000000")) with
            { ProductName = "Renamed", ProductSku = "NEW", MinimumStock = 9 };

        var outcomes = await Task.WhenAll(Apply(winner), Apply(lower), Apply(winner));
        Assert.Equal(2, outcomes.Count(r => r == ProjectionResult.Applied));
        Assert.Single(outcomes, r => r == ProjectionResult.Duplicate);
        await Apply(early);
        var tiedId = Guid.Parse("00000002-0000-0000-0000-000000000000");
        await Apply(lower with { EventId = tiedId, MovementId = tiedId });

        await using var db = CreateContext(tenant);
        Assert.Equal(winner.ProjectLevel(), await db.StockLevels.SingleAsync());
        Assert.Equal(4, await db.StockMovements.CountAsync());
        Assert.Equal(4, await db.ProcessedEvents.CountAsync());

        var greatestId = Guid.Parse("10000000-0000-0000-0000-000000000000");
        var laterTie = winner with { EventId = greatestId, MovementId = greatestId, PreviousQuantity = 6, NewQuantity = 4 };
        await Apply(laterTie);
        Assert.Equal(laterTie.ProjectLevel(), await db.StockLevels.AsNoTracking().SingleAsync());
    }

    [PostgreSqlFact]
    public async Task FailedMovementInsertRollsBackUpsertAndLedgerAndCanBeRetried()
    {
        var first = Movement(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Day, 0, 5);
        await Apply(first);
        var second = Movement(first.TenantId, first.StockId, first.BranchId, first.ProductId, Day.AddDays(1), 5, 2);
        await using var db = CreateContext(first.TenantId);
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE stock_movements ADD CONSTRAINT test_reject_issues CHECK ("Type" <> 'Issue')
            """);
        await Assert.ThrowsAsync<DbUpdateException>(() => Apply(second));
        Assert.Equal(first.ProjectLevel(), await db.StockLevels.AsNoTracking().SingleAsync());
        Assert.Equal(first.EventId, (await db.ProcessedEvents.SingleAsync()).EventId);
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE stock_movements DROP CONSTRAINT test_reject_issues");
        Assert.Equal(ProjectionResult.Applied, await Apply(second));
        Assert.Equal(second.ProjectLevel(), await db.StockLevels.AsNoTracking().SingleAsync());
        Assert.Equal(2, await db.StockMovements.CountAsync());
    }

    [PostgreSqlFact]
    public async Task MovementReportsIncludeReversalsAndGroupByTypeUtcDayAndProduct()
    {
        var tenant = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var stock = Guid.NewGuid();
        var product = Guid.NewGuid();
        var other = Guid.NewGuid();
        var end = Day.AddDays(1);
        await Apply(Movement(tenant, stock, branch, product, Day, 10, 7));
        await Apply(Movement(tenant, stock, branch, product, end, 7, 10));
        await Apply(Movement(tenant, Guid.NewGuid(), branch, other, end, 4, 3) with { Type = "Adjustment" });
        await Apply(Movement(tenant, stock, branch, product, Day.AddSeconds(-1), 0, 10));
        await Apply(Movement(tenant, Guid.NewGuid(), Guid.NewGuid(), product, Day, 0, 9));
        await Apply(Movement(Guid.NewGuid(), stock, branch, product, Day, 0, 20));
        await using var db = CreateContext(tenant);
        var result = await new InventoryReports(db).MovementsAsync(new(Day, end, branch), default);
        Assert.Equal(new[] { new MovementTypeTotals("Adjustment", 1, 1, -1), new("Issue", 1, 3, -3), new("Receipt", 1, 3, 3) },
            result.ByType);
        Assert.Equal(new[] { new MovementDayTotals(Day, 1, 3, -3), new(end, 2, 4, 2) }, result.ByDay);
        Assert.Equal(2, result.ByProduct.Count);
        Assert.Contains(new MovementProductTotals(product, 2, 6, 0), result.ByProduct);
        Assert.Contains(new MovementProductTotals(other, 1, 1, -1), result.ByProduct);
    }

    [PostgreSqlFact]
    public async Task LowStockIncludesOldLevelsAndPaginatesByShortageThenStockIdWithinTenantAndBranch()
    {
        var tenant = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var product = Guid.NewGuid();
        var first = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var second = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var urgent = Guid.NewGuid();
        await Apply(Movement(tenant, second, branch, product, Day, 0, 4));
        await Apply(Movement(tenant, first, branch, product, Day, 0, 4));
        await Apply(Movement(tenant, urgent, branch, product, Day, 1, 0));
        await Apply(Movement(tenant, Guid.NewGuid(), branch, product, Day, 0, 10));
        await Apply(Movement(tenant, Guid.NewGuid(), branch, product, Day, 1, 0) with { MinimumStock = 0 });
        await Apply(Movement(tenant, Guid.NewGuid(), Guid.NewGuid(), product, Day, 1, 0));
        await Apply(Movement(Guid.NewGuid(), first, branch, product, Day.AddYears(1), 0, 20));

        await using var db = CreateContext(tenant);
        var reports = new InventoryReports(db);
        var page = await reports.LowStockAsync(branch, 1, 2, default);
        Assert.Equal(3, page.Total);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal(new[] { new LowStock(urgent, product, "Hammer", "A", branch, 0, 10, 10),
            new(first, product, "Hammer", "A", branch, 4, 10, 6) }, page.Items);
        Assert.Equal(second, Assert.Single((await reports.LowStockAsync(branch, 2, 2, default)).Items).StockId);
        Assert.Empty((await reports.LowStockAsync(branch, int.MaxValue, 100, default)).Items);
        Assert.Equal(4, (await reports.LowStockAsync(null, 1, 20, default)).Total);
    }
}
