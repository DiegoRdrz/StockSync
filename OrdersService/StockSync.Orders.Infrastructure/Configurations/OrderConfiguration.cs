using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockSync.Orders.Domain.Common;
using StockSync.Orders.Domain.Entities;

namespace StockSync.Orders.Infrastructure.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", t =>
        {
            t.HasCheckConstraint("CK_Orders_Status", EnumCheck.In<OrderStatus>("Status"));
            t.HasCheckConstraint("CK_Orders_Total", "\"Total\" >= 0");
        });

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.Notes).HasMaxLength(Order.NotesMaxLength);
        builder.Property(o => o.ReviewReason).HasMaxLength(Order.ReviewReasonMaxLength);
        builder.Property(o => o.Total).HasPrecision(18, Amount.DecimalPlaces);

        // Lines belong to the order: they are saved, loaded and deleted with it.
        builder.HasMany(o => o.Lines)
            .WithOne()
            .HasForeignKey(l => l.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(o => o.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property<Guid>(OrdersDbContext.TenantIdProperty);

        // BranchId refers to another service's branch; there is no branch entity or foreign key.
        // Indexes support the list filters, ordered by creation date within each tenant.
        builder.HasIndex(OrdersDbContext.TenantIdProperty, nameof(Order.Status), nameof(Order.CreatedAt));
        builder.HasIndex(OrdersDbContext.TenantIdProperty, nameof(Order.BranchId), nameof(Order.CreatedAt));
        builder.HasIndex(OrdersDbContext.TenantIdProperty, nameof(Order.CreatedAt));
    }
}
