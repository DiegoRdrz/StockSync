using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockSync.Identity.Domain.Entities;

namespace StockSync.Identity.Infrastructure.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.TenantId)
            .IsRequired();

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(User.EmailMaxLength);

        builder.Property(u => u.PasswordHash)
            .IsRequired();

        builder.Property(u => u.FirstName)
            .IsRequired()
            .HasMaxLength(User.FirstNameMaxLength);

        builder.Property(u => u.LastName)
            .IsRequired()
            .HasMaxLength(User.LastNameMaxLength);

        builder.Property(u => u.Role)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(u => u.IsActive)
            .IsRequired();

        builder.Property(u => u.CreatedAt)
            .IsRequired();

        // Unique index on active email
        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasFilter("\"IsActive\"");

        builder.HasIndex(u => u.TenantId);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

