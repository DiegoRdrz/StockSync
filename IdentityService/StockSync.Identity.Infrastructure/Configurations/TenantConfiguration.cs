using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockSync.Identity.Domain.Entities;

namespace StockSync.Identity.Infrastructure.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(Tenant.NameMaxLength);

        builder.Property(t => t.TaxId)
            .HasMaxLength(Tenant.TaxIdMaxLength);

        builder.Property(t => t.IsActive)
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        // Unique index on active tenant names
        builder.HasIndex(t => t.Name)
            .IsUnique()
            .HasFilter("\"IsActive\"");
    }
}

