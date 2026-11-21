using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace StockSync.Notification.Infrastructure.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Domain.Entities.Notification>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.Notification> builder)
    {
        builder.ToTable("Notifications");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();

        builder.Property(n => n.SourceEventId).IsRequired();
        builder.Property(n => n.EventType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(n => n.Channel).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(n => n.Recipient).IsRequired().HasMaxLength(Domain.Entities.Notification.RecipientMaxLength);
        builder.Property(n => n.Subject).IsRequired().HasMaxLength(Domain.Entities.Notification.SubjectMaxLength);
        builder.Property(n => n.Body).IsRequired().HasMaxLength(Domain.Entities.Notification.BodyMaxLength);
        builder.Property(n => n.BranchId).IsRequired();
        builder.Property(n => n.ResourceId).IsRequired();
        builder.Property(n => n.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(n => n.CreatedAt).IsRequired();
        builder.Property(n => n.FailureReason).HasMaxLength(Domain.Entities.Notification.FailureReasonMaxLength);

        builder.Property<Guid>(NotificationDbContext.TenantIdProperty);

        builder.HasIndex(NotificationDbContext.TenantIdProperty, nameof(Domain.Entities.Notification.CreatedAt));
        builder.HasIndex(
            NotificationDbContext.TenantIdProperty,
            nameof(Domain.Entities.Notification.BranchId),
            nameof(Domain.Entities.Notification.CreatedAt));
        builder.HasIndex(NotificationDbContext.TenantIdProperty, nameof(Domain.Entities.Notification.SourceEventId));
    }
}
