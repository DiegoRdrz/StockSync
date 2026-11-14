using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Infrastructure.Configurations;

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements", table =>
        {
            table.HasCheckConstraint("CK_StockMovements_Quantity", "\"Quantity\" > 0");
            table.HasCheckConstraint("CK_StockMovements_Balances", "\"PreviousQuantity\" >= 0 AND \"NewQuantity\" >= 0");
            table.HasCheckConstraint("CK_StockMovements_Type", "\"Type\" IN ('Receipt', 'Issue', 'Adjustment')");
        });
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Type).HasConversion<string>().HasMaxLength(10);
        builder.HasOne<Stock>().WithMany().HasForeignKey(m => m.StockId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(m => new { m.StockId, m.Date, m.Id });
        builder.Property<Guid>(InventoryDbContext.TenantIdProperty);
        // Support the general movement list, ordered by date within each tenant.
        builder.HasIndex(InventoryDbContext.TenantIdProperty, nameof(StockMovement.Date), nameof(StockMovement.Id));
    }
}
