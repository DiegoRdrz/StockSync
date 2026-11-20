using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StockSync.Inventory.Application.IntegrationEvents;

namespace StockSync.Inventory.Infrastructure.Outbox;

public sealed record StockMovementBackfillResult(long Examined, long Enqueued, long AlreadyExisting, long Skipped);

public sealed class StockMovementEventBackfill(InventoryDbContext context, ILogger<StockMovementEventBackfill> logger)
{
    private const int BatchSize = 200;

    public async Task<StockMovementBackfillResult> RunAsync(CancellationToken cancellationToken = default)
    {
        long examined = 0, enqueued = 0, alreadyExisting = 0, skipped = 0;
        Guid? after = null;

        while (true)
        {
            var source = after is { } cursor
                ? context.StockMovements.FromSql($"SELECT * FROM \"StockMovements\" WHERE \"Id\" > {cursor}")
                : context.StockMovements;
            var page = source.IgnoreQueryFilters().OrderBy(m => m.Id).Take(BatchSize);

            var batch = await (
                from movement in page
                let tenantId = EF.Property<Guid>(movement, InventoryDbContext.TenantIdProperty)
                join stock in context.Stocks.IgnoreQueryFilters()
                    on new { Id = movement.StockId, TenantId = tenantId }
                    equals new { stock.Id, TenantId = EF.Property<Guid>(stock, InventoryDbContext.TenantIdProperty) } into stocks
                from stock in stocks.DefaultIfEmpty()
                join product in context.Products.IgnoreQueryFilters()
                    on new { Id = stock.ProductId, TenantId = tenantId }
                    equals new { product.Id, TenantId = EF.Property<Guid>(product, InventoryDbContext.TenantIdProperty) } into products
                from product in products.DefaultIfEmpty()
                orderby movement.Id
                select new { Movement = movement, Stock = stock, Product = product, TenantId = tenantId })
                .AsNoTracking().ToListAsync(cancellationToken);

            if (batch.Count == 0)
                break;

            var messages = new List<OutboxMessage>(batch.Count);
            foreach (var row in batch)
            {
                if (row.Stock is null || row.Product is null)
                {
                    skipped++;
                    logger.LogWarning("Skipping stock movement {MovementId} for tenant {TenantId}: product could not be resolved.",
                        row.Movement.Id, row.TenantId);
                    continue;
                }

                messages.Add(OutboxMessage.From(StockMovementRecorded.From(row.Movement, row.Stock, row.Product, row.TenantId)));
            }

            var inserted = messages.Count == 0 ? 0 : await InsertAsync(messages, cancellationToken);
            examined += batch.Count;
            enqueued += inserted;
            alreadyExisting += messages.Count - inserted;
            after = batch[^1].Movement.Id;
        }

        logger.LogInformation("Stock movement backfill: examined {Examined}, enqueued {Enqueued}, already existing {AlreadyExisting}, skipped {Skipped}.",
            examined, enqueued, alreadyExisting, skipped);
        return new(examined, enqueued, alreadyExisting, skipped);
    }

    private Task<int> InsertAsync(List<OutboxMessage> messages, CancellationToken cancellationToken)
    {
        var ids = messages.Select(m => m.Id).ToArray();
        var tenants = messages.Select(m => m.TenantId).ToArray();
        var dates = messages.Select(m => m.OccurredAt).ToArray();
        var payloads = messages.Select(m => m.Payload).ToArray();

        // The database decides which IDs are new, even across concurrent backfill workers.
        return context.Database.ExecuteSqlAsync($"""
            INSERT INTO "OutboxMessages" ("Id", "TenantId", "OccurredAt", "EventType", "Payload", "Attempts")
            SELECT id, tenant_id, occurred_at, {StockMovementRecorded.Name}, payload::jsonb, 0
            FROM unnest({ids}, {tenants}, {dates}, {payloads}) AS batch(id, tenant_id, occurred_at, payload)
            ORDER BY id
            ON CONFLICT ("Id") DO NOTHING
            """, cancellationToken);
    }
}
