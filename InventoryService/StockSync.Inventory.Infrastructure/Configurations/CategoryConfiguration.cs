using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Infrastructure.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Name).IsRequired().HasMaxLength(Category.NameMaxLength);
        builder.Property(c => c.NormalizedName).IsRequired().HasMaxLength(Category.NameMaxLength);
        builder.Property(c => c.Description).HasMaxLength(Category.DescriptionMaxLength);
        builder.Property<Guid>(InventoryDbContext.TenantIdProperty);

        // Soft deletion: names must be unique only among active categories within a tenant.
        builder.HasIndex(InventoryDbContext.TenantIdProperty, nameof(Category.NormalizedName))
            .IsUnique()
            .HasFilter("\"IsActive\"");
    }
}
