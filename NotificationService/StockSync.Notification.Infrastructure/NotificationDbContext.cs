using Microsoft.EntityFrameworkCore;
using StockSync.Notification.Application.Common;
using StockSync.Notification.Domain.Entities;

namespace StockSync.Notification.Infrastructure;

public class NotificationDbContext : DbContext
{
    // Shadow property: multitenancy belongs to persistence, not the domain.
    public const string TenantIdProperty = "TenantId";

    private readonly ICurrentTenant? _currentTenant;

    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
    {
    }

    public NotificationDbContext(DbContextOptions<NotificationDbContext> options, ICurrentTenant currentTenant) : base(options)
    {
        _currentTenant = currentTenant;
    }

    // Evaluate per query so global filters use the current request or event tenant.
    public Guid TenantId => _currentTenant?.TenantId ?? DefaultTenant.Id;

    public DbSet<Domain.Entities.Notification> Notifications => Set<Domain.Entities.Notification>();
    public DbSet<NotificationRecipient> NotificationRecipients => Set<NotificationRecipient>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationDbContext).Assembly);

        FilterByTenant<Domain.Entities.Notification>(modelBuilder);
        FilterByTenant<NotificationRecipient>(modelBuilder);
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
            if (entry.Metadata.FindProperty(TenantIdProperty) is not null)
                entry.Property(TenantIdProperty).CurrentValue = TenantId;
        }
    }
}
