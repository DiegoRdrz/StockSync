using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Infrastructure.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Name).IsRequired().HasMaxLength(Product.NameMaxLength);
        builder.Property(p => p.Sku).IsRequired().HasMaxLength(Product.SkuMaxLength);
        builder.Property(p => p.Description).HasMaxLength(Product.DescriptionMaxLength);
        builder.Property(p => p.PurchasePrice).HasPrecision(18, Product.PriceDecimalPlaces);
        builder.Property(p => p.SalePrice).HasPrecision(18, Product.PriceDecimalPlaces);

        builder.Property<Guid>(InventoryDbContext.TenantIdProperty);

        // Soft deletion: SKUs must be unique only among active products within a tenant.
        builder.HasIndex(InventoryDbContext.TenantIdProperty, nameof(Product.Sku))
            .IsUnique()
            .HasFilter("\"IsActive\"");
        builder.HasIndex(p => p.CategoryId);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
