using Microsoft.EntityFrameworkCore;
using StockSync.Inventory.Application.Categories;
using StockSync.Inventory.Application.Common;
using StockSync.Inventory.Application.Common.Exceptions;
using StockSync.Inventory.Application.Movements;
using StockSync.Inventory.Application.Products;
using StockSync.Inventory.Application.Stocks;
using StockSync.Inventory.Infrastructure;
using StockSync.Inventory.Infrastructure.Repositories;

namespace StockSync.Inventory.IntegrationTests;

public class MultitenancyPostgresTests : PostgresTestBase
{
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();

    private static ProductService CreateProductService(InventoryDbContext context) =>
        new(new ProductRepository(context), new CategoryRepository(context));

    private static StockService CreateStockService(InventoryDbContext context) =>
        new(new StockRepository(context), new ProductRepository(context), new StockMovementRepository(context),
            new UnitOfWork(context));

    private static StockMovementService CreateMovementService(InventoryDbContext context) =>
        new(new StockRepository(context), new ProductRepository(context), new StockMovementRepository(context),
            new UnitOfWork(context));

    [PostgreSqlFact]
    public async Task TenantData_CannotBeReadOrReferencedByAnotherTenant()
    {
        Guid categoryA, productA, stockA;
        await using (var contextA = CreateContext(_tenantA))
        {
            categoryA = (await new CategoryService(new CategoryRepository(contextA)).CreateAsync(new("Tools", null), default)).Id;
            productA = (await CreateProductService(contextA).CreateAsync(new("Hammer", "FER-001", null, 1m, 2m, 5, categoryA), default)).Id;
            stockA = (await CreateStockService(contextA).CreateAsync(new(productA, Guid.NewGuid(), 3), default)).Id;
        }

        await using var contextB = CreateContext(_tenantB);
        var categoriesB = new CategoryService(new CategoryRepository(contextB));
        var productsB = CreateProductService(contextB);
        var stocksB = CreateStockService(contextB);

        await Assert.ThrowsAsync<NotFoundException>(() => categoriesB.GetByIdAsync(categoryA, default));
        await Assert.ThrowsAsync<NotFoundException>(() => productsB.GetByIdAsync(productA, default));
        await Assert.ThrowsAsync<NotFoundException>(() => stocksB.GetByIdAsync(stockA, default));
        await Assert.ThrowsAsync<NotFoundException>(() => CreateMovementService(contextB).RecordIssueAsync(stockA, new(1), default));
        Assert.Equal(0, (await productsB.ListAsync(new ProductFilter(), default)).Total);
        Assert.Equal(0, (await stocksB.ListLowStockAsync(new LowStockFilter(), default)).Total);
        Assert.Equal(0, (await CreateMovementService(contextB).ListAllAsync(1, default)).Total);
        await Assert.ThrowsAsync<ValidationException>(() =>
            productsB.CreateAsync(new("Other", "FER-002", null, 1m, 2m, 0, categoryA), default));
        await Assert.ThrowsAsync<ValidationException>(() =>
            stocksB.CreateAsync(new(productA, Guid.NewGuid(), 1), default));

        var categoryB = await categoriesB.CreateAsync(new("Tools", null), default);
        await productsB.CreateAsync(new("Hammer", "FER-001", null, 1m, 2m, 0, categoryB.Id), default);

        await using var verification = CreateContext(_tenantA);
        Assert.Equal(1, await verification.Products.CountAsync());
        var tenantsBySku = await verification.Products.IgnoreQueryFilters()
            .Where(p => p.Sku == "FER-001")
            .Select(p => EF.Property<Guid>(p, InventoryDbContext.TenantIdProperty))
            .ToListAsync();
        Assert.Equal(new[] { _tenantA, _tenantB }.Order(), tenantsBySku.Order());
        Assert.Equal(_tenantA, await verification.StockMovements
            .Select(m => EF.Property<Guid>(m, InventoryDbContext.TenantIdProperty)).SingleAsync());
    }

    [PostgreSqlFact]
    public async Task ContextWithoutTenant_UsesDefaultTenant()
    {
        await using (var withoutTenant = CreateContext())
            await new CategoryService(new CategoryRepository(withoutTenant)).CreateAsync(new("Demo", null), default);

        await using var defaultValue = CreateContext(DefaultTenant.Id);
        await using var otherProduct = CreateContext(_tenantA);
        Assert.Equal(1, await defaultValue.Categories.CountAsync());
        Assert.Equal(0, await otherProduct.Categories.CountAsync());
    }
}
