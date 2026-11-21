using Microsoft.EntityFrameworkCore;
using StockSync.Inventory.Application.Common.Exceptions;
using StockSync.Inventory.Application.Movements;
using StockSync.Inventory.Application.Products;
using StockSync.Inventory.Application.Stocks;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Exceptions;
using StockSync.Inventory.Infrastructure;
using StockSync.Inventory.Infrastructure.Outbox;
using StockSync.Inventory.Infrastructure.Repositories;

namespace StockSync.Inventory.IntegrationTests;

public class StockPostgresTests : PostgresTestBase
{
    private static StockService CreateStockService(InventoryDbContext context) =>
        new(new StockRepository(context), new ProductRepository(context), new StockMovementRepository(context),
            new EfIntegrationEventOutbox(context), new TestTenant(context.TenantId), new UnitOfWork(context));

    private static StockMovementService CreateMovementService(InventoryDbContext context) =>
        new(new StockRepository(context), new ProductRepository(context), new StockMovementRepository(context),
            new EfIntegrationEventOutbox(context), new TestTenant(context.TenantId), new UnitOfWork(context));

    private async Task<Product> CreateProductAsync(string sku = "FER-001", int minimumStock = 0)
    {
        await using var context = CreateContext();
        var product = Product.Create($"Product {sku}", sku, null, 1m, 2m, minimumStock, null);
        context.Products.Add(product);
        await context.SaveChangesAsync();
        return product;
    }

    [PostgreSqlFact]
    public async Task InitialBalanceAndAdjustments_AreRecordedAndMatchBalance()
    {
        var product = await CreateProductAsync();
        await using var context = CreateContext();
        var stockService = CreateStockService(context);

        var stock = await stockService.CreateAsync(new(product.Id, Guid.NewGuid(), 10), default);
        await CreateMovementService(context).RecordIssueAsync(stock.Id, new(3), default);
        await stockService.UpdateQuantityAsync(stock.Id, new(20), default);
        await stockService.UpdateQuantityAsync(stock.Id, new(20), default);

        await using var verification = CreateContext();
        var movements = await verification.StockMovements
            .Where(m => m.StockId == stock.Id).OrderBy(m => m.Date).ToListAsync();
        var balance = (await verification.Stocks.SingleAsync(s => s.Id == stock.Id)).Quantity;
        Assert.Equal(
            new[] { StockMovementType.Adjustment, StockMovementType.Issue, StockMovementType.Adjustment },
            movements.Select(m => m.Type));
        Assert.Equal(20, balance);
        Assert.Equal(balance, movements.Sum(m => m.NewQuantity - m.PreviousQuantity));
    }

    // Optimistic locking alone rejected requests even when sufficient stock remained.
    [PostgreSqlFact]
    public async Task ConcurrentSales_ApplySequentiallyWithoutOversellingOrConflicts()
    {
        var product = await CreateProductAsync();
        Guid stockId;
        await using (var context = CreateContext())
            stockId = (await CreateStockService(context).CreateAsync(new(product.Id, Guid.NewGuid(), 10), default)).Id;

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(async () =>
        {
            await using var context = CreateContext();
            try
            {
                await CreateMovementService(context).RecordIssueAsync(stockId, new(1), default);
                return true;
            }
            catch (InsufficientStockException)
            {
                return false;
            }
        })));

        Assert.Equal(10, results.Count(sold => sold));
        await using var verification = CreateContext();
        Assert.Equal(0, (await verification.Stocks.SingleAsync()).Quantity);
        var issues = await verification.StockMovements
            .Where(m => m.Type == StockMovementType.Issue).OrderBy(m => m.PreviousQuantity).ToListAsync();
        Assert.Equal(Enumerable.Range(1, 10), issues.Select(m => m.PreviousQuantity));
    }

    [PostgreSqlFact]
    public async Task ListByBranch_ExcludesInactiveProductsAndPaginates()
    {
        var branch = Guid.NewGuid();
        var activeItems = new[] { await CreateProductAsync("A-1"), await CreateProductAsync("A-2") };
        var inactive = await CreateProductAsync("B-1");
        await using var context = CreateContext();
        var stockService = CreateStockService(context);
        foreach (var product in activeItems.Append(inactive))
            await stockService.CreateAsync(new(product.Id, branch, 0), default);
        await new ProductService(new ProductRepository(context), new CategoryRepository(context))
            .DeleteAsync(inactive.Id, default);

        var page1 = await stockService.ListByBranchAsync(branch, new StockFilter { PageSize = 1 }, default);
        var page2 = await stockService.ListByBranchAsync(branch, new StockFilter { Page = 2, PageSize = 1 }, default);

        Assert.Equal(2, page1.Total);
        Assert.Equal(activeItems.Select(p => p.Id),
            page1.Items.Concat(page2.Items).Select(s => s.ProductId));
        await Assert.ThrowsAsync<NotFoundException>(
            () => stockService.ListByProductAsync(inactive.Id, new StockFilter(), default));
    }

    [PostgreSqlFact]
    public async Task LowStock_ListsOnlyBelowMinimumQuantitiesOrderedByShortage()
    {
        var branch = Guid.NewGuid();
        var otherBranch = Guid.NewGuid();
        var minimumFive = await CreateProductAsync("MIN-5", minimumStock: 5);
        var minimumTen = await CreateProductAsync("MIN-10", minimumStock: 10);
        var withoutMinimum = await CreateProductAsync("MIN-0", minimumStock: 0);
        await using var context = CreateContext();
        var stockService = CreateStockService(context);
        var minorAlert = await stockService.CreateAsync(new(minimumFive.Id, branch, 3), default);
        await stockService.CreateAsync(new(minimumFive.Id, otherBranch, 5), default);
        var criticalAlert = await stockService.CreateAsync(new(minimumTen.Id, otherBranch, 1), default);
        await stockService.CreateAsync(new(withoutMinimum.Id, branch, 0), default);

        var allItems = await stockService.ListLowStockAsync(new LowStockFilter(), default);
        var byBranch = await stockService.ListLowStockAsync(new LowStockFilter { BranchId = branch }, default);

        Assert.Equal(new[] { criticalAlert.Id, minorAlert.Id }, allItems.Items.Select(a => a.StockId));
        Assert.Equal(new[] { 9, 2 }, allItems.Items.Select(a => a.Shortage));
        Assert.Equal("MIN-10", allItems.Items[0].ProductSku);
        Assert.Equal(minorAlert.Id, Assert.Single(byBranch.Items).StockId);
    }

    [PostgreSqlFact]
    public async Task DeleteProductAndStock_OnlyWithoutStock()
    {
        var product = await CreateProductAsync();
        await using var context = CreateContext();
        var productService = new ProductService(new ProductRepository(context), new CategoryRepository(context));
        var stockService = CreateStockService(context);
        var withStock = await stockService.CreateAsync(new(product.Id, Guid.NewGuid(), 5), default);
        var withoutStock = await stockService.CreateAsync(new(product.Id, Guid.NewGuid(), 0), default);

        await Assert.ThrowsAsync<ConflictException>(() => productService.DeleteAsync(product.Id, default));
        await Assert.ThrowsAsync<ConflictException>(() => stockService.DeleteAsync(withStock.Id, default));
        await stockService.DeleteAsync(withoutStock.Id, default);
        await CreateMovementService(context).RecordIssueAsync(withStock.Id, new(5), default);
        await productService.DeleteAsync(product.Id, default);

        await using var verification = CreateContext();
        Assert.False((await verification.Products.SingleAsync()).IsActive);
        Assert.Equal(withStock.Id, (await verification.Stocks.SingleAsync()).Id);
    }
}
