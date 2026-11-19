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
    }
}
