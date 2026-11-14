using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StockSync.Inventory.Infrastructure;

namespace StockSync.Inventory.IntegrationTests;

public class EnglishMigrationPostgresTests : PostgresTestBase
{
    [PostgreSqlFact]
    public async Task UpgradeAndRollback_PreserveSeededDataAndMovementTypes()
    {
        await using var context = CreateContext();
        await InventoryDbSeeder.SeedAsync(context);
        var categories = await context.Categories.AsNoTracking().OrderBy(c => c.Id)
            .Select(c => new { c.Id, c.Name, c.NormalizedName, c.IsActive,
                TenantId = EF.Property<Guid>(c, InventoryDbContext.TenantIdProperty) }).ToListAsync();
        var products = await context.Products.AsNoTracking().OrderBy(p => p.Id)
            .Select(p => new { p.Id, p.Name, p.Sku, p.CategoryId,
                TenantId = EF.Property<Guid>(p, InventoryDbContext.TenantIdProperty) }).ToListAsync();
        var stocks = await context.Stocks.AsNoTracking().OrderBy(s => s.Id)
            .Select(s => new { s.Id, s.ProductId, s.BranchId, s.Quantity,
                TenantId = EF.Property<Guid>(s, InventoryDbContext.TenantIdProperty) }).ToListAsync();
        var movements = await context.StockMovements.AsNoTracking().OrderBy(m => m.Id)
            .Select(m => new { m.Id, m.StockId, m.Type, m.Quantity, m.PreviousQuantity, m.NewQuantity, m.Date,
                TenantId = EF.Property<Guid>(m, InventoryDbContext.TenantIdProperty) }).ToListAsync();

        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("20261007035505_Multitenencia");
        var legacyTypes = await context.Database.SqlQueryRaw<string>(
            "SELECT DISTINCT \"Tipo\" AS \"Value\" FROM \"MovimientosStock\"").ToListAsync();
        Assert.Equal(new[] { "Ajuste", "Entrada", "Salida" }, legacyTypes.OrderBy(t => t));

        await migrator.MigrateAsync();
        Assert.Equal(categories, await context.Categories.AsNoTracking().OrderBy(c => c.Id)
            .Select(c => new { c.Id, c.Name, c.NormalizedName, c.IsActive,
                TenantId = EF.Property<Guid>(c, InventoryDbContext.TenantIdProperty) }).ToListAsync());
        Assert.Equal(products, await context.Products.AsNoTracking().OrderBy(p => p.Id)
            .Select(p => new { p.Id, p.Name, p.Sku, p.CategoryId,
                TenantId = EF.Property<Guid>(p, InventoryDbContext.TenantIdProperty) }).ToListAsync());
        Assert.Equal(stocks, await context.Stocks.AsNoTracking().OrderBy(s => s.Id)
            .Select(s => new { s.Id, s.ProductId, s.BranchId, s.Quantity,
                TenantId = EF.Property<Guid>(s, InventoryDbContext.TenantIdProperty) }).ToListAsync());
        Assert.Equal(movements, await context.StockMovements.AsNoTracking().OrderBy(m => m.Id)
            .Select(m => new { m.Id, m.StockId, m.Type, m.Quantity, m.PreviousQuantity, m.NewQuantity, m.Date,
                TenantId = EF.Property<Guid>(m, InventoryDbContext.TenantIdProperty) }).ToListAsync());
        Assert.False(context.Database.HasPendingModelChanges());
    }
}
