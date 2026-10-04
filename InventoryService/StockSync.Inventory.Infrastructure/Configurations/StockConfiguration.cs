using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Infrastructure.Configurations;

public class StockConfiguration : IEntityTypeConfiguration<Stock>
{
    public void Configure(EntityTypeBuilder<Stock> builder)
    {
        builder.ToTable("Stocks");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        // Los productos solo se dan de baja (Activo = false), nunca se borran: Restrict evita perder asignaciones por accidente.
        builder.HasOne<Producto>()
            .WithMany()
            .HasForeignKey(s => s.ProductoId)
            .OnDelete(DeleteBehavior.Restrict);

        // SucursalId es el identificador de una sucursal de otro servicio: sin FK ni entidad Sucursal.
        builder.HasIndex(s => new { s.ProductoId, s.SucursalId }).IsUnique();
        builder.HasIndex(s => s.SucursalId);
    }
}
