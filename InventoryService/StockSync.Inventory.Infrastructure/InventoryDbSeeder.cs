using Microsoft.EntityFrameworkCore;
using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Infrastructure;

public static class InventoryDbSeeder
{
    public static async Task SeedAsync(InventoryDbContext context, CancellationToken cancellationToken = default)
    {
        var tools = await GetCategoryAsync("Demo Tools");
        var stationery = await GetCategoryAsync("Demo Stationery");
        var mainBranch = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var secondaryBranch = Guid.Parse("22222222-2222-2222-2222-222222222222");

        await AddProductAndStockAsync("Demo hammer", "DEMO-HER-001", 10, 15, 5,
            tools.Id, mainBranch, 0, 10, 3);
        await AddProductAndStockAsync("Demo drill", "DEMO-HER-002", 50, 75, 2,
            tools.Id, mainBranch, 0, 5, 5);
        await AddProductAndStockAsync("Demo notebook", "DEMO-PAP-001", 2, 4, 3,
            stationery.Id, secondaryBranch, 12, 0, 0);

        // One SaveChanges atomically persists categories, products, balances and history.
        await context.SaveChangesAsync(cancellationToken);

        async Task<Category> GetCategoryAsync(string name)
        {
            var normalized = Category.NormalizeName(name);
            var category = await context.Categories.FirstOrDefaultAsync(
                c => c.IsActive && c.NormalizedName == normalized, cancellationToken);
            if (category is not null)
                return category;

            category = Category.Create(name, "Test category");
            context.Categories.Add(category);
            return category;
        }

        async Task AddProductAndStockAsync(string name, string sku, decimal purchase, decimal sale,
            int minimum, Guid categoryId, Guid branchId, int initialBalance, int entry, int issue)
        {
            // A SKU can occur on inactive records and one active record; prefer the active record.
            var product = await context.Products
                .OrderByDescending(p => p.IsActive)
                .FirstOrDefaultAsync(p => p.Sku == sku, cancellationToken);
            if (product is null)
            {
                product = Product.Create(name, sku, "Test product", purchase, sale, minimum, categoryId);
                context.Products.Add(product);
            }

            // Include inactive records without reactivating or overwriting them.
            if (!product.IsActive || await context.Stocks.AnyAsync(
                    s => s.ProductId == product.Id && s.BranchId == branchId, cancellationToken))
                return;

            var stock = Stock.Create(product.Id, branchId, 0);
            context.Stocks.Add(stock);
            if (stock.UpdateQuantity(initialBalance) is { } initialAdjustment)
                context.StockMovements.Add(initialAdjustment);
            if (entry > 0)
                context.StockMovements.Add(stock.RecordMovement(StockMovementType.Receipt, entry));
            if (issue > 0)
                context.StockMovements.Add(stock.RecordMovement(StockMovementType.Issue, issue));
        }
    }
}
