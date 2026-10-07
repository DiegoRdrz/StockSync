using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Infrastructure.Configurations;

public class MovimientoStockConfiguration : IEntityTypeConfiguration<MovimientoStock>
{
    public void Configure(EntityTypeBuilder<MovimientoStock> builder)
    {
        builder.ToTable("MovimientosStock", table =>
        {
            table.HasCheckConstraint("CK_MovimientosStock_Cantidad", "\"Cantidad\" > 0");
            table.HasCheckConstraint("CK_MovimientosStock_Saldos", "\"CantidadAnterior\" >= 0 AND \"CantidadPosterior\" >= 0");
            table.HasCheckConstraint("CK_MovimientosStock_Tipo", "\"Tipo\" IN ('Entrada', 'Salida', 'Ajuste')");
        });
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Tipo).HasConversion<string>().HasMaxLength(10);
        builder.HasOne<Stock>().WithMany().HasForeignKey(m => m.StockId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(m => new { m.StockId, m.Fecha, m.Id });
        builder.Property<Guid>(InventoryDbContext.TenantIdPropiedad);
        // Sirve al listado general de movimientos, que se ordena por fecha dentro de cada tenant.
        builder.HasIndex(InventoryDbContext.TenantIdPropiedad, nameof(MovimientoStock.Fecha), nameof(MovimientoStock.Id));
    }
}
