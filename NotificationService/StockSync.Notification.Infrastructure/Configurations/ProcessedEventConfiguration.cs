using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace StockSync.Notification.Infrastructure.Configurations;

public class ProcessedEventConfiguration : IEntityTypeConfiguration<ProcessedEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedEvent> builder)
    {
        builder.ToTable("ProcessedEvents");

        builder.HasKey(e => new { e.ConsumerName, e.EventId })
            .HasName("PK_ProcessedEvents");

        builder.Property(e => e.ConsumerName).IsRequired().HasMaxLength(100);
        builder.Property(e => e.EventId).IsRequired();
        builder.Property(e => e.ProcessedAt).IsRequired();
    }
}
