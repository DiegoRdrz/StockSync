using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockSync.Notification.Domain.Entities;

namespace StockSync.Notification.Infrastructure.Configurations;

public class NotificationRecipientConfiguration : IEntityTypeConfiguration<NotificationRecipient>
{
    public void Configure(EntityTypeBuilder<NotificationRecipient> builder)
    {
        builder.ToTable("NotificationRecipients");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Name).IsRequired().HasMaxLength(NotificationRecipient.NameMaxLength);
        builder.Property(r => r.Channel).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(r => r.Destination).IsRequired().HasMaxLength(NotificationRecipient.DestinationMaxLength);
        builder.Property(r => r.NormalizedDestination).IsRequired().HasMaxLength(NotificationRecipient.DestinationMaxLength);
        builder.Property(r => r.IsActive).IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();

        builder.Property<Guid>(NotificationDbContext.TenantIdProperty);

        // Soft deletion: destinations must be unique only among active recipients per channel within a tenant.
        builder.HasIndex(
                NotificationDbContext.TenantIdProperty,
                nameof(NotificationRecipient.Channel),
                nameof(NotificationRecipient.NormalizedDestination))
            .IsUnique()
            .HasFilter("\"IsActive\"");
    }
}
