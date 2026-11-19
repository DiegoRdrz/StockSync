using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockSync.Orders.Domain.Entities;

namespace StockSync.Orders.Infrastructure.Configurations;

public class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> builder)
    {
        StockLineConfiguration.Configure(builder, "PurchaseOrderLines");

        builder.HasIndex(l => new { l.PurchaseOrderId, l.ProductId }).IsUnique();
        builder.HasIndex(l => new { l.PurchaseOrderId, l.LineNumber }).IsUnique();
    }
}
