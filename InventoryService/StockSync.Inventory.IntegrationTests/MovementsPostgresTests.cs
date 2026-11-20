using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockSync.Inventory.Application.Common.Exceptions;
using StockSync.Inventory.Application.Movements;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Infrastructure;
using StockSync.Inventory.Infrastructure.Outbox;
using StockSync.Inventory.Infrastructure.Repositories;

namespace StockSync.Inventory.IntegrationTests;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STOCKSYNC_TEST_POSTGRES")))
            Skip = "Set STOCKSYNC_TEST_POSTGRES to run against PostgreSQL (see README).";
    }
}

// Each test creates and deletes its own database, never the application's database.
public class MovementsPostgresTests : IAsyncLifetime
{
    private readonly string _database = $"stocksync_test_{Guid.NewGuid():N}";
    private string _connectionString = null!;
    private Guid _stockId;

    public async Task InitializeAsync()
    {
        var adminConnection = Environment.GetEnvironmentVariable("STOCKSYNC_TEST_POSTGRES")!;
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_database}\"", admin);
        await command.ExecuteNonQueryAsync();
        _connectionString = new NpgsqlConnectionStringBuilder(adminConnection) { Database = _database, Pooling = false }.ConnectionString;

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var product = Product.Create("Sample", "TEST", null, 1, 2, 0, null);
        var stock = Stock.Create(product.Id, Guid.NewGuid(), 10);
        context.Products.Add(product);
        context.Stocks.Add(stock);
        await context.SaveChangesAsync();
        _stockId = stock.Id;
    }

    public async Task DisposeAsync()
    {
        await using var admin = new NpgsqlConnection(Environment.GetEnvironmentVariable("STOCKSYNC_TEST_POSTGRES"));
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)", admin);
        await command.ExecuteNonQueryAsync();
    }

    private InventoryDbContext CreateContext() => new(
        new DbContextOptionsBuilder<InventoryDbContext>().UseNpgsql(_connectionString).Options);

    [PostgreSqlFact]
    public async Task ListAll_PagesByTenIncludingInactiveProducts()
    {
        await using var context = CreateContext();
        await InventoryDbSeeder.SeedAsync(context);
        var stock = await context.Stocks.SingleAsync(s => s.Id == _stockId);
        for (var i = 0; i < 12; i++)
            context.StockMovements.Add(stock.RecordMovement(StockMovementType.Receipt, 1));
        var hammer = await context.Products.SingleAsync(p => p.Sku == "DEMO-HER-001");
        hammer.Deactivate();
        await context.SaveChangesAsync();
        var service = new StockMovementService(new StockRepository(context), new ProductRepository(context),
            new StockMovementRepository(context), new EfIntegrationEventOutbox(context),
            new TestTenant(context.TenantId), new UnitOfWork(context));

        var page1 = await service.ListAllAsync(1, default);
        var page2 = await service.ListAllAsync(2, default);
        Assert.Equal(10, page1.Items.Count);
        Assert.Equal(7, page2.Items.Count);
        Assert.Equal(10, page1.PageSize);
        Assert.Equal(17, page1.Total);
        Assert.Equal(17, page2.Total);
        Assert.Equal(2, page2.TotalPages);
        var expectedItems = await context.StockMovements.OrderByDescending(m => m.Date)
            .ThenByDescending(m => m.Id).Select(m => m.Id).ToListAsync();
        var allItems = page1.Items.Concat(page2.Items).ToList();
        Assert.Equal(expectedItems, allItems.Select(m => m.Id));
        var hammerStock = allItems.Where(m => m.ProductId == hammer.Id).ToList();
        Assert.Equal(2, hammerStock.Count);
        Assert.All(hammerStock, m =>
        {
            Assert.Equal("Demo hammer", m.ProductName);
            Assert.Equal("DEMO-HER-001", m.ProductSku);
        });
        Assert.Empty((await service.ListAllAsync(3, default)).Items);
        await Assert.ThrowsAsync<ValidationException>(() => service.ListAllAsync(0, default));
        await Assert.ThrowsAsync<ValidationException>(() => service.ListAllAsync(int.MaxValue, default));
    }

    [PostgreSqlFact]
    public async Task Seeding_Repeated_PreservesDataBalancesAndHistory()
    {
        Guid hammerId;
        Guid stockId;
        await using (var context = CreateContext())
        {
            await InventoryDbSeeder.SeedAsync(context);
            Assert.Equal(2, await context.Categories.CountAsync());
            Assert.Equal(4, await context.Products.CountAsync()); // Includes the fixture's product.
            Assert.Equal(4, await context.Stocks.CountAsync());
            Assert.Equal(5, await context.StockMovements.CountAsync());
            var hammer = await context.Products.SingleAsync(p => p.Sku == "DEMO-HER-001");
            hammerId = hammer.Id;
            var stock = await context.Stocks.SingleAsync(s => s.ProductId == hammerId);
            stockId = stock.Id;
            Assert.Equal(7, stock.Quantity);
            var drillId = (await context.Products.SingleAsync(p => p.Sku == "DEMO-HER-002")).Id;
            Assert.Equal(0, (await context.Stocks.SingleAsync(s => s.ProductId == drillId)).Quantity);
            context.StockMovements.Add(stock.RecordMovement(StockMovementType.Issue, 2));
            hammer.Deactivate();
            await context.SaveChangesAsync();
        }

        await using var repeated = CreateContext();
        await InventoryDbSeeder.SeedAsync(repeated);
        Assert.Equal(2, await repeated.Categories.CountAsync());
        Assert.Equal(4, await repeated.Products.CountAsync());
        Assert.Equal(4, await repeated.Stocks.CountAsync());
        Assert.Equal(6, await repeated.StockMovements.CountAsync());
        Assert.Equal(5, (await repeated.Stocks.SingleAsync(s => s.Id == stockId)).Quantity);
        Assert.False((await repeated.Products.SingleAsync(p => p.Id == hammerId)).IsActive);
    }

    [PostgreSqlFact]
    public async Task TwoSalesWithSameBalance_OnlyOneCommitsWithAtomicHistory()
    {
        await using var first = CreateContext();
        await using var second = CreateContext();
        var repo1 = new StockRepository(first);
        var repo2 = new StockRepository(second);
        // Force both clients to read 10 before either commits a sale of 7.
        var stock1 = (await repo1.GetForUpdateAsync(_stockId, default))!;
        var stock2 = (await repo2.GetForUpdateAsync(_stockId, default))!;
        await new StockMovementRepository(first).AddAsync(stock1.RecordMovement(StockMovementType.Issue, 7), default);
        await new StockMovementRepository(second).AddAsync(stock2.RecordMovement(StockMovementType.Issue, 7), default);

        var results = await Task.WhenAll(TrySave(repo1), TrySave(repo2));

        Assert.Single(results.Where(ok => ok));
        await using var verification = CreateContext();
        Assert.Equal(3, (await verification.Stocks.SingleAsync()).Quantity);
        var movement = await verification.StockMovements.SingleAsync();
        Assert.Equal(10, movement.PreviousQuantity);
        Assert.Equal(3, movement.NewQuantity);
    }

    [PostgreSqlFact]
    public async Task ManualAdjustmentWithStaleBalance_DoesNotOverwriteSale()
    {
        await using var adjustment = CreateContext();
        await using var sale = CreateContext();
        var repoAdjustment = new StockRepository(adjustment);
        var stockAdjustment = (await repoAdjustment.GetForUpdateAsync(_stockId, default))!;
        var saleRepository = new StockRepository(sale);
        var saleStock = (await saleRepository.GetForUpdateAsync(_stockId, default))!;
        sale.StockMovements.Add(saleStock.RecordMovement(StockMovementType.Issue, 4));
        await saleRepository.SaveChangesAsync(default);

        stockAdjustment.UpdateQuantity(20);
        await Assert.ThrowsAsync<ConflictException>(() => repoAdjustment.SaveChangesAsync(default));
        await using var verification = CreateContext();
        Assert.Equal(6, (await verification.Stocks.SingleAsync()).Quantity);
        Assert.Equal(1, await verification.StockMovements.CountAsync());
    }

    [PostgreSqlFact]
    public async Task DeleteStockWithMovements_RejectsAndPreservesHistory()
    {
        await using (var entry = CreateContext())
        {
            var stock = await entry.Stocks.SingleAsync();
            entry.StockMovements.Add(stock.RecordMovement(StockMovementType.Receipt, 2));
            await entry.SaveChangesAsync();
        }

        await using (var deleted = CreateContext())
        {
            var repo = new StockRepository(deleted);
            repo.Delete((await repo.GetForUpdateAsync(_stockId, default))!);
            await Assert.ThrowsAsync<ConflictException>(() => repo.SaveChangesAsync(default));
        }

        await using var verification = CreateContext();
        Assert.Equal(12, (await verification.Stocks.SingleAsync()).Quantity);
        Assert.Equal(1, await verification.StockMovements.CountAsync());
    }

    [PostgreSqlFact]
    public async Task DatabaseConstraint_PreventsNegativeStock()
    {
        await using var context = CreateContext();
        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlRawAsync("UPDATE \"Stocks\" SET \"Quantity\" = -1"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal(10, (await context.Stocks.SingleAsync()).Quantity);
    }

    private static async Task<bool> TrySave(StockRepository repo)
    {
        try
        {
            await repo.SaveChangesAsync(default);
            return true;
        }
        catch (ConflictException)
        {
            return false;
        }
    }
}
