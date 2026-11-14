using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Infrastructure.Configurations;

public class StockConfiguration : IEntityTypeConfiguration<Stock>
{
    public void Configure(EntityTypeBuilder<Stock> builder)
    {
        builder.ToTable("Stocks", table =>
            table.HasCheckConstraint("CK_Stocks_Quantity", "\"Quantity\" >= 0"));

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        // Every write, including manual adjustments and deletion, starts from the observed balance.
        // EF includes the original quantity in WHERE to detect concurrent updates.
        builder.Property(s => s.Quantity).IsConcurrencyToken();

        // Products are deactivated, never deleted; Restrict prevents accidental assignment loss.
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(s => s.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property<Guid>(InventoryDbContext.TenantIdProperty);

        // BranchId refers to another service's branch; there is no branch entity or foreign key.
        // A product belongs to one tenant, so product/branch uniqueness does not need TenantId.
        builder.HasIndex(s => new { s.ProductId, s.BranchId }).IsUnique();
        builder.HasIndex(InventoryDbContext.TenantIdProperty, nameof(Stock.BranchId));
    }
}
