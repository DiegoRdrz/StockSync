using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockSync.Orders.Domain.Common;
using StockSync.Orders.Domain.Entities;

namespace StockSync.Orders.Infrastructure.Configurations;

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("PurchaseOrders", t =>
        {
            t.HasCheckConstraint("CK_PurchaseOrders_Status", EnumCheck.In<PurchaseOrderStatus>("Status"));
            t.HasCheckConstraint("CK_PurchaseOrders_Total", "\"Total\" >= 0");
        });

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.SupplierName).IsRequired().HasMaxLength(PurchaseOrder.SupplierNameMaxLength);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Notes).HasMaxLength(PurchaseOrder.NotesMaxLength);
        builder.Property(p => p.ReviewReason).HasMaxLength(PurchaseOrder.ReviewReasonMaxLength);
        builder.Property(p => p.Total).HasPrecision(18, Amount.DecimalPlaces);

        builder.HasMany(p => p.Lines)
            .WithOne()
            .HasForeignKey(l => l.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property<Guid>(OrdersDbContext.TenantIdProperty);

        builder.HasIndex(OrdersDbContext.TenantIdProperty, nameof(PurchaseOrder.Status), nameof(PurchaseOrder.CreatedAt));
        builder.HasIndex(OrdersDbContext.TenantIdProperty, nameof(PurchaseOrder.BranchId), nameof(PurchaseOrder.CreatedAt));
        builder.HasIndex(OrdersDbContext.TenantIdProperty, nameof(PurchaseOrder.CreatedAt));
    }
}
