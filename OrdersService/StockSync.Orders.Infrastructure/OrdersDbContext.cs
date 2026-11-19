using Microsoft.EntityFrameworkCore;
using StockSync.Orders.Application.Common;
using StockSync.Orders.Domain.Entities;
using StockSync.Orders.Infrastructure.Outbox;

namespace StockSync.Orders.Infrastructure;

public class OrdersDbContext : DbContext
{
    // Shadow property: multitenancy belongs to persistence, not the domain.
    public const string TenantIdProperty = "TenantId";

    private readonly ICurrentTenant? _currentTenant;

    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options)
    {
    }

    public OrdersDbContext(DbContextOptions<OrdersDbContext> options, ICurrentTenant currentTenant) : base(options)
    {
        _currentTenant = currentTenant;
    }

    // Evaluate per query so global filters use the current request tenant.
    public Guid TenantId => _currentTenant?.TenantId ?? DefaultTenant.Id;

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    // Not filtered by tenant: the publisher sends events of every tenant.
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrdersDbContext).Assembly);

        FilterByTenant<Order>(modelBuilder);
        FilterByTenant<OrderLine>(modelBuilder);
        FilterByTenant<PurchaseOrder>(modelBuilder);
        FilterByTenant<PurchaseOrderLine>(modelBuilder);
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

    // Only the shadow property is assigned; OutboxMessage stores its event's tenant explicitly.
    private void AssignTenantToNewEntities()
    {
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Added))
        {
            if (entry.Metadata.FindProperty(TenantIdProperty) is { } property && property.IsShadowProperty())
                entry.Property(TenantIdProperty).CurrentValue = TenantId;
        }
    }
}
