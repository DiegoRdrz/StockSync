using Microsoft.EntityFrameworkCore;
using StockSync.Inventory.Application.Categories;
using StockSync.Inventory.Application.Common.Exceptions;
using StockSync.Inventory.Application.Products;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Infrastructure;
using StockSync.Inventory.Infrastructure.Repositories;

namespace StockSync.Inventory.IntegrationTests;

public class ActiveOnlyUniquenessPostgresTests : PostgresTestBase
{
    [PostgreSqlFact]
    public async Task Category_RecreateAndRenameUsingInactiveName_IsAllowed()
    {
        await using var context = CreateContext();
        var service = new CategoryService(new CategoryRepository(context));
        var original = await service.CreateAsync(new("Chocodsa", null), default);
        await service.DeleteAsync(original.Id, default);

        var recreated = await service.CreateAsync(new("Chocodsa", null), default);
        var otherCategory = await service.CreateAsync(new("Temporary", null), default);
        await service.DeleteAsync(otherCategory.Id, default);
        var renamed = await service.UpdateAsync(recreated.Id, new("Temporary", null), default);

        Assert.NotEqual(original.Id, recreated.Id);
        Assert.Equal("Temporary", renamed.Name);
        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(new(" temporary ", null), default));
    }

    [PostgreSqlFact]
    public async Task Product_ReuseInactiveSku_IsAllowed()
    {
        await using var context = CreateContext();
        var service = new ProductService(new ProductRepository(context), new CategoryRepository(context));
        ProductRequest Request(string sku) => new("Hammer", sku, null, 10m, 15m, 0, null);
        var original = await service.CreateAsync(Request("FER-001"), default);
        await service.DeleteAsync(original.Id, default);

        var newProduct = await service.CreateAsync(Request("fer-001"), default);
        var otherProduct = await service.CreateAsync(Request("FER-002"), default);
        await service.DeleteAsync(otherProduct.Id, default);
        var updatedProduct = await service.UpdateAsync(newProduct.Id, Request("FER-002"), default);

        Assert.NotEqual(original.Id, newProduct.Id);
        Assert.Equal("FER-002", updatedProduct.Sku);
        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(Request("FER-002"), default));
    }

    // The filtered index must reject duplicate active records even without service validation.
    [PostgreSqlFact]
    public async Task UniqueIndexes_RejectActiveDuplicates()
    {
        await using (var context = CreateContext())
        {
            var inactiveCategory = Category.Create("Chocodsa", null);
            inactiveCategory.Deactivate();
            var inactiveProduct = Product.Create("Old", "SKU-1", null, 1m, 2m, 0, null);
            inactiveProduct.Deactivate();
            context.AddRange(inactiveCategory, Category.Create("Chocodsa", null),
                inactiveProduct, Product.Create("New", "SKU-1", null, 1m, 2m, 0, null));
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext())
        {
            var repo = new CategoryRepository(context);
            await repo.AddAsync(Category.Create(" CHOCODSA ", null), default);
            await Assert.ThrowsAsync<ConflictException>(() => repo.SaveChangesAsync(default));
        }

        await using (var context = CreateContext())
        {
            var repo = new ProductRepository(context);
            await repo.AddAsync(Product.Create("Other", "SKU-1", null, 1m, 2m, 0, null), default);
            await Assert.ThrowsAsync<ConflictException>(() => repo.SaveChangesAsync(default));
        }
    }

    [PostgreSqlFact]
    public async Task Seeding_WithReusedSkuAndInactiveCategory_SucceedsWithoutUsingInactiveRecords()
    {
        await using (var context = CreateContext())
        {
            var inactiveCategory = Category.Create("Demo Tools", null);
            inactiveCategory.Deactivate();
            var inactiveHammer = Product.Create("Old hammer", "DEMO-HER-001", null, 1m, 2m, 0, null);
            inactiveHammer.Deactivate();
            context.AddRange(inactiveCategory, inactiveHammer,
                Product.Create("New hammer", "DEMO-HER-001", null, 1m, 2m, 0, null));
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext())
        {
            await InventoryDbSeeder.SeedAsync(context);
            await InventoryDbSeeder.SeedAsync(context);
        }

        await using var verification = CreateContext();
        var productCategories = await verification.Products
            .Where(p => p.CategoryId != null)
            .Join(verification.Categories, p => p.CategoryId, c => c.Id, (p, c) => c.IsActive)
            .ToListAsync();
        Assert.NotEmpty(productCategories);
        Assert.All(productCategories, Assert.True);
        var activeHammer = await verification.Products.SingleAsync(p => p.Sku == "DEMO-HER-001" && p.IsActive);
        Assert.Equal("New hammer", activeHammer.Name);
        Assert.Equal(1, await verification.Stocks.CountAsync(s => s.ProductId == activeHammer.Id));
        Assert.Equal(2, await verification.Products.CountAsync(p => p.Sku == "DEMO-HER-001"));
    }
}
