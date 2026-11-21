using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StockSync.Inventory.Application.IntegrationEvents;
using StockSync.Inventory.Application.Movements;
using StockSync.Inventory.Application.Stocks;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Infrastructure;
using StockSync.Inventory.Infrastructure.Messaging;
using StockSync.Inventory.Infrastructure.Outbox;
using StockSync.Inventory.Infrastructure.Repositories;

namespace StockSync.Inventory.IntegrationTests;

public class OutboxPostgresTests : PostgresTestBase
{
    private static StockService StockServiceFor(InventoryDbContext context) =>
        new(new StockRepository(context), new ProductRepository(context), new StockMovementRepository(context),
            new EfIntegrationEventOutbox(context), new TestTenant(context.TenantId), new UnitOfWork(context));

    private static StockMovementService MovementServiceFor(InventoryDbContext context) =>
        new(new StockRepository(context), new ProductRepository(context), new StockMovementRepository(context),
            new EfIntegrationEventOutbox(context), new TestTenant(context.TenantId), new UnitOfWork(context));

    [PostgreSqlFact]
    public async Task LiveMovements_PersistOneEventPerMovementWithItsTenantAndHistoricalBalances()
    {
        var tenant = Guid.NewGuid();
        var branch = Guid.NewGuid();
        await using (var context = CreateContext(tenant))
        {
            var product = Product.Create("Hammer", "HAM-1", null, 1m, 2m, 4, null);
            context.Products.Add(product);
            await context.SaveChangesAsync();
            var stocks = StockServiceFor(context);
            var movements = MovementServiceFor(context);
            var stock = await stocks.CreateAsync(new(product.Id, branch, 6), default);
            await stocks.UpdateQuantityAsync(stock.Id, new(8), default);
            await movements.RecordReceiptAsync(stock.Id, new(3), default);
            await movements.RecordIssueAsync(stock.Id, new(2), default);
            await stocks.UpdateQuantityAsync(stock.Id, new(9), default);
        }

        await using var stored = CreateContext(tenant);
        var history = await stored.StockMovements.OrderBy(m => m.PreviousQuantity).ToListAsync();
        Assert.Equal(4, history.Count);
        Assert.Equal(new[] { (0, 6), (6, 8), (8, 11), (11, 9) },
            history.Select(m => (m.PreviousQuantity, m.NewQuantity)));
        Assert.Equal(9, (await stored.Stocks.SingleAsync()).Quantity);

        await using var publisherContext = CreateContext();
        var outbox = await publisherContext.OutboxMessages.ToDictionaryAsync(m => m.Id);
        Assert.Equal(history.Count, outbox.Count);
        foreach (var movement in history)
        {
            var row = outbox[movement.Id];
            var payload = ReadPayload(row);
            Assert.Equal(tenant, row.TenantId);
            Assert.Equal(tenant, payload.TenantId);
            Assert.Equal(StockMovementRecorded.Name, row.EventType);
            Assert.Equal(movement.Id, payload.EventId);
            Assert.Equal(movement.Id, payload.MovementId);
            Assert.Equal(movement.Date, row.OccurredAt);
            Assert.Equal(movement.Date, payload.OccurredAt);
            Assert.Equal(movement.Date, payload.MovementDate);
            Assert.Equal((movement.PreviousQuantity, movement.NewQuantity),
                (payload.PreviousQuantity, payload.NewQuantity));
            Assert.Equal(movement.Type.ToString(), payload.Type);
            Assert.Equal(branch, payload.BranchId);
            Assert.Null(row.ProcessedAt);
        }
    }

    [PostgreSqlFact]
    public async Task RollbackOfBusinessTransaction_RemovesMovementAndEvent()
    {
        await using (var context = CreateContext())
        {
            var product = Product.Create("Hammer", "HAM-1", null, 1m, 2m, 4, null);
            context.Products.Add(product);
            await context.SaveChangesAsync();
            var stock = await StockServiceFor(context).CreateAsync(new(product.Id, Guid.NewGuid(), 0), default);

            await using var transaction = await context.Database.BeginTransactionAsync();
            var receipt = await MovementServiceFor(context).RecordReceiptAsync(stock.Id, new(3), default);
            Assert.Equal(receipt.Id, (await context.OutboxMessages.SingleAsync()).Id);

            await using var concurrentReader = CreateContext();
            Assert.Empty(await concurrentReader.OutboxMessages.ToListAsync());
            Assert.Empty(await concurrentReader.StockMovements.ToListAsync());
            Assert.Equal(0, (await concurrentReader.Stocks.SingleAsync()).Quantity);
            await transaction.RollbackAsync();
        }

        await using var stored = CreateContext();
        Assert.Empty(await stored.OutboxMessages.ToListAsync());
        Assert.Empty(await stored.StockMovements.ToListAsync());
        Assert.Equal(0, (await stored.Stocks.SingleAsync()).Quantity);
    }

    [PostgreSqlFact]
    public async Task EfOutbox_PreservesExplicitTenantOutsideItsRequestScope()
    {
        var owner = Guid.NewGuid();
        await using var context = CreateContext();
        var message = CreateEvent(owner, DateTime.UtcNow);
        new EfIntegrationEventOutbox(context).Enqueue(message);
        await context.SaveChangesAsync();

        var stored = await context.OutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal(owner, stored.TenantId);
        Assert.Equal(owner, ReadPayload(stored).TenantId);
    }

    [PostgreSqlFact]
    public async Task Publisher_MarksConfirmedRowsAndResumesAfterFailure()
    {
        var startedAt = DateTime.UtcNow;
        var events = Enumerable.Range(0, 3).Select(i => CreateEvent(Guid.NewGuid(), startedAt.AddSeconds(i))).ToArray();
        await using (var context = CreateContext())
        {
            var outbox = new EfIntegrationEventOutbox(context);
            foreach (var integrationEvent in events)
                outbox.Enqueue(integrationEvent);
            await context.SaveChangesAsync();
        }

        var transport = new RecordingPublisher { FailOn = events[1].EventId };
        await using var services = new ServiceCollection()
            .AddDbContext<InventoryDbContext>(options => options.UseNpgsql(ConnectionString))
            .BuildServiceProvider();
        using var publisher = new OutboxPublisher(services.GetRequiredService<IServiceScopeFactory>(), transport,
            NullLogger<OutboxPublisher>.Instance);

        Assert.Equal(1, await publisher.PublishPendingAsync(default));
        await using var check = CreateContext();
        var firstPass = await check.OutboxMessages.AsNoTracking().OrderBy(m => m.OccurredAt).ToListAsync();
        Assert.NotNull(firstPass[0].ProcessedAt);
        Assert.Null(firstPass[1].ProcessedAt);
        Assert.Equal(1, firstPass[1].Attempts);
        Assert.Equal("Broker unavailable.", firstPass[1].LastError);
        Assert.Equal(0, firstPass[2].Attempts);

        transport.FailOn = null;
        Assert.Equal(2, await publisher.PublishPendingAsync(default));
        Assert.Equal(events.Select(e => e.EventId), transport.Published);
        var secondPass = await check.OutboxMessages.AsNoTracking().OrderBy(m => m.OccurredAt).ToListAsync();
        Assert.All(secondPass, row => { Assert.NotNull(row.ProcessedAt); Assert.Null(row.LastError); });
        Assert.Equal(2, secondPass[1].Attempts);
        Assert.Equal(0, await publisher.PublishPendingAsync(default));
    }

    internal static StockMovementRecorded ReadPayload(OutboxMessage message) =>
        Assert.IsType<StockMovementRecorded>(JsonSerializer.Deserialize<StockMovementRecorded>(
            message.Payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

    private static StockMovementRecorded CreateEvent(Guid tenant, DateTime date)
    {
        var product = Product.Create("Hammer", "HAM-1", null, 1m, 2m, 4, null);
        var stock = Stock.Create(product.Id, Guid.NewGuid(), 0);
        var movement = stock.RecordMovement(StockMovementType.Receipt, 1);
        return StockMovementRecorded.From(movement, stock, product, tenant) with { OccurredAt = date, MovementDate = date };
    }

    private sealed class RecordingPublisher : IMessagePublisher
    {
        public Guid? FailOn { get; set; }
        public List<Guid> Published { get; } = [];

        public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
        {
            if (message.Id == FailOn)
                throw new InvalidOperationException("Broker unavailable.");
            Published.Add(message.Id);
            return Task.CompletedTask;
        }
    }
}
