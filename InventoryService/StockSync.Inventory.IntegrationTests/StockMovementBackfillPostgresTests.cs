using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StockSync.Inventory.Application.IntegrationEvents;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Infrastructure.Outbox;

namespace StockSync.Inventory.IntegrationTests;

public class StockMovementBackfillPostgresTests : PostgresTestBase
{
    [PostgreSqlFact]
    public async Task Backfill_PreservesSourceTenantAndHistoryAcrossPagesAndConcurrentRuns()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var first = await SeedHistoryAsync(tenantA, 201);
        var second = await SeedHistoryAsync(tenantB, 2);

        await using (var context = CreateContext(tenantB))
        {
            var product = await context.Products.SingleAsync();
            product.Update("Renamed hammer", "NEW-2", null, 1m, 2m, 15, null);
            product.Deactivate();
            await context.SaveChangesAsync();
        }

        async Task<StockMovementBackfillResult> BackfillAsync()
        {
            await using var context = CreateContext();
            return await new StockMovementEventBackfill(context, NullLogger<StockMovementEventBackfill>.Instance).RunAsync();
        }

        var concurrent = await Task.WhenAll(BackfillAsync(), BackfillAsync());
        Assert.All(concurrent, result => { Assert.Equal(203, result.Examined); Assert.Equal(0, result.Skipped); });
        Assert.Equal(203, concurrent.Sum(r => r.Enqueued));
        Assert.Equal(203, concurrent.Sum(r => r.AlreadyExisting));
        Assert.Equal(new StockMovementBackfillResult(203, 0, 203, 0), await BackfillAsync());

        await using var reader = CreateContext();
        var messages = await reader.OutboxMessages.ToDictionaryAsync(m => m.Id);
        Assert.Equal(203, messages.Count);
        foreach (var (movements, tenant, sku, name, minimum) in new[]
        {
            (first, tenantA, "HAM-1", "Hammer", 4),
            (second, tenantB, "NEW-2", "Renamed hammer", 15)
        })
        {
            foreach (var movement in movements)
            {
                var row = messages[movement.Id];
                var payload = OutboxPostgresTests.ReadPayload(row);
                Assert.Equal(tenant, row.TenantId);
                Assert.Equal(tenant, payload.TenantId);
                Assert.Equal(movement.Id, payload.EventId);
                Assert.Equal(movement.Id, payload.MovementId);
                Assert.Equal(movement.Date, row.OccurredAt);
                Assert.Equal(movement.Date, payload.OccurredAt);
                Assert.Equal(movement.Date, payload.MovementDate);
                Assert.Equal((movement.PreviousQuantity, movement.NewQuantity),
                    (payload.PreviousQuantity, payload.NewQuantity));
                Assert.Equal((sku, name, minimum), (payload.ProductSku, payload.ProductName, payload.MinimumStock));
            }
        }
    }

    [PostgreSqlFact]
    public async Task Backfill_SkipsMovementWithNoProductInItsTenant()
    {
        var owner = Guid.NewGuid();
        await SeedHistoryAsync(owner, 1);
        await using var context = CreateContext();
        // The product FK still resolves by ID, but the row cannot supply metadata for another tenant.
        await context.Database.ExecuteSqlAsync($"""
            UPDATE "Products" SET "TenantId" = {Guid.NewGuid()} WHERE "TenantId" = {owner}
            """);

        var result = await new StockMovementEventBackfill(context, NullLogger<StockMovementEventBackfill>.Instance).RunAsync();

        Assert.Equal(new StockMovementBackfillResult(1, 0, 0, 1), result);
        Assert.Empty(await context.OutboxMessages.ToListAsync());
    }

    private async Task<List<StockMovement>> SeedHistoryAsync(Guid tenant, int count)
    {
        await using var context = CreateContext(tenant);
        var product = Product.Create("Hammer", "HAM-1", null, 1m, 2m, 4, null);
        var stock = Stock.Create(product.Id, Guid.NewGuid(), 0);
        context.Products.Add(product);
        context.Stocks.Add(stock);
        var movements = new List<StockMovement>();
        for (var i = 0; i < count; i++)
            movements.Add(stock.RecordMovement(StockMovementType.Receipt, 1));
        context.StockMovements.AddRange(movements);
        await context.SaveChangesAsync();
        return movements;
    }
}
