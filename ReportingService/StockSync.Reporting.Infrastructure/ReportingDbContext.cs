using Microsoft.EntityFrameworkCore;
using StockSync.Reporting.Application.Common;
using StockSync.Reporting.Domain;

namespace StockSync.Reporting.Infrastructure;

public sealed record ProcessedEvent(string ConsumerName, Guid EventId, DateTime ProcessedAt);

public sealed class ReportingDbContext(DbContextOptions<ReportingDbContext> options, ICurrentTenant tenant) : DbContext(options)
{
    public Guid TenantId => tenant.TenantId;
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleLine> SaleLines => Set<SaleLine>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseLine> PurchaseLines => Set<PurchaseLine>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<StockLevel> StockLevels => Set<StockLevel>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        var processed = model.Entity<ProcessedEvent>();
        processed.ToTable("processed_events");
        processed.HasKey(e => new { e.ConsumerName, e.EventId }).HasName("pk_processed_events");

        var sale = model.Entity<Sale>();
        sale.ToTable("sales");
        sale.Property<Guid>("TenantId").ValueGeneratedNever();
        sale.HasKey("TenantId", nameof(Sale.OrderId));
        sale.HasIndex("TenantId", nameof(Sale.ConfirmedAt), nameof(Sale.OrderId));
        sale.HasIndex("TenantId", nameof(Sale.BranchId), nameof(Sale.ConfirmedAt));
        sale.HasQueryFilter(s => EF.Property<Guid>(s, "TenantId") == TenantId);

        var line = model.Entity<SaleLine>();
        line.ToTable("sale_lines");
        line.Property<Guid>("TenantId").ValueGeneratedNever();
        line.HasKey("TenantId", nameof(SaleLine.OrderId), nameof(SaleLine.LineNumber));
        line.HasIndex("TenantId", nameof(SaleLine.ProductId));
        line.HasQueryFilter(l => EF.Property<Guid>(l, "TenantId") == TenantId);
        sale.HasMany(s => s.Lines).WithOne().HasForeignKey("TenantId", nameof(SaleLine.OrderId));

        var purchase = model.Entity<Purchase>();
        purchase.ToTable("purchases");
        purchase.Property<Guid>("TenantId").ValueGeneratedNever();
        purchase.HasKey("TenantId", nameof(Purchase.PurchaseOrderId));
        purchase.HasIndex("TenantId", nameof(Purchase.ReceivedAt));
        purchase.HasIndex("TenantId", nameof(Purchase.BranchId), nameof(Purchase.ReceivedAt));
        purchase.HasQueryFilter(p => EF.Property<Guid>(p, "TenantId") == TenantId);

        var purchaseLine = model.Entity<PurchaseLine>();
        purchaseLine.ToTable("purchase_lines");
        purchaseLine.Property<Guid>("TenantId").ValueGeneratedNever();
        purchaseLine.HasKey("TenantId", nameof(PurchaseLine.PurchaseOrderId), nameof(PurchaseLine.LineNumber));
        purchaseLine.HasQueryFilter(l => EF.Property<Guid>(l, "TenantId") == TenantId);
        purchase.HasMany(p => p.Lines).WithOne().HasForeignKey("TenantId", nameof(PurchaseLine.PurchaseOrderId));

        var movement = model.Entity<StockMovement>();
        movement.ToTable("stock_movements");
        movement.Property<Guid>("TenantId").ValueGeneratedNever();
        movement.HasKey(m => m.MovementId);
        movement.Property(m => m.MovementId).ValueGeneratedNever();
        movement.HasIndex("TenantId", nameof(StockMovement.MovementDate));
        movement.HasIndex("TenantId", nameof(StockMovement.BranchId), nameof(StockMovement.MovementDate));
        movement.HasQueryFilter(m => EF.Property<Guid>(m, "TenantId") == TenantId);

        var level = model.Entity<StockLevel>();
        level.ToTable("stock_levels");
        level.Property<Guid>("TenantId").ValueGeneratedNever();
        level.HasKey("TenantId", nameof(StockLevel.StockId));
        level.HasIndex("TenantId", nameof(StockLevel.BranchId))
            .HasFilter("\"MinimumStock\" > 0 AND \"Quantity\" < \"MinimumStock\"");
        level.HasQueryFilter(l => EF.Property<Guid>(l, "TenantId") == TenantId);
    }
}
