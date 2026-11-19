using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockSync.Orders.Domain.Entities;

namespace StockSync.Orders.Infrastructure.Configurations;

public class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> builder)
    {
        StockLineConfiguration.Configure(builder, "OrderLines");

        // A product appears once per order; quantity changes update the existing line.
        builder.HasIndex(l => new { l.OrderId, l.ProductId }).IsUnique();
        builder.HasIndex(l => new { l.OrderId, l.LineNumber }).IsUnique();
    }
}
