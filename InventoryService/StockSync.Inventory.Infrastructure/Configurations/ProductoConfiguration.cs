using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Infrastructure.Configurations;

public class ProductoConfiguration : IEntityTypeConfiguration<Producto>
{
    public void Configure(EntityTypeBuilder<Producto> builder)
    {
        builder.ToTable("Productos");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Nombre).IsRequired().HasMaxLength(Producto.NombreMaxLength);
        builder.Property(p => p.Sku).IsRequired().HasMaxLength(Producto.SkuMaxLength);
        builder.Property(p => p.Descripcion).HasMaxLength(Producto.DescripcionMaxLength);
        builder.Property(p => p.PrecioCompra).HasPrecision(18, 2);
        builder.Property(p => p.PrecioVenta).HasPrecision(18, 2);

        // La baja es lógica: el SKU solo debe ser único entre productos activos.
        builder.HasIndex(p => p.Sku).IsUnique().HasFilter("\"Activo\"");
        builder.HasIndex(p => p.CategoriaId);

        builder.HasOne<Categoria>()
            .WithMany()
            .HasForeignKey(p => p.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
