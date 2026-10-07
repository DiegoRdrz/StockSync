using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Infrastructure.Configurations;

public class StockConfiguration : IEntityTypeConfiguration<Stock>
{
    public void Configure(EntityTypeBuilder<Stock> builder)
    {
        builder.ToTable("Stocks", table =>
            table.HasCheckConstraint("CK_Stocks_Cantidad", "\"Cantidad\" >= 0"));

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        // Toda escritura (también el ajuste manual y el borrado) debe partir del saldo leído.
        // EF incluye la cantidad original en el WHERE y detecta actualizaciones concurrentes.
        builder.Property(s => s.Cantidad).IsConcurrencyToken();

        // Los productos solo se dan de baja (Activo = false), nunca se borran: Restrict evita perder asignaciones por accidente.
        builder.HasOne<Producto>()
            .WithMany()
            .HasForeignKey(s => s.ProductoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property<Guid>(InventoryDbContext.TenantIdPropiedad);

        // SucursalId es el identificador de una sucursal de otro servicio: sin FK ni entidad Sucursal.
        // El producto ya pertenece a un único tenant, así que la unicidad producto-sucursal no lo necesita.
        builder.HasIndex(s => new { s.ProductoId, s.SucursalId }).IsUnique();
        builder.HasIndex(InventoryDbContext.TenantIdPropiedad, nameof(Stock.SucursalId));
    }
}
