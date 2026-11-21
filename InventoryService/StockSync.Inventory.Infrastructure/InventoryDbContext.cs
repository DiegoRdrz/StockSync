using Microsoft.EntityFrameworkCore;
using StockSync.Inventory.Application.Common;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Infrastructure.Outbox;

namespace StockSync.Inventory.Infrastructure;

public class InventoryDbContext : DbContext
{
    // Shadow property: multitenancy belongs to persistence, not the domain.
    public const string TenantIdProperty = "TenantId";

    private readonly ICurrentTenant? _currentTenant;

    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options)
    {
    }

    public InventoryDbContext(DbContextOptions<InventoryDbContext> options, ICurrentTenant currentTenant) : base(options)
    {
        _currentTenant = currentTenant;
    }

    // Evaluate per query so global filters use the current request tenant.
    public Guid TenantId => _currentTenant?.TenantId ?? DefaultTenant.Id;

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InventoryDbContext).Assembly);

        FilterByTenant<Category>(modelBuilder);
        FilterByTenant<Product>(modelBuilder);
        FilterByTenant<Stock>(modelBuilder);
        FilterByTenant<StockMovement>(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AssignTenantToNewEntities();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AssignTenantToNewEntities();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void FilterByTenant<TEntity>(ModelBuilder modelBuilder) where TEntity : class =>
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => EF.Property<Guid>(e, TenantIdProperty) == TenantId);

    private void AssignTenantToNewEntities()
    {
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Added))
        {
            if (entry.Metadata.FindProperty(TenantIdProperty)?.IsShadowProperty() == true)
                entry.Property(TenantIdProperty).CurrentValue = TenantId;
        }
    }
}
