using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Infrastructure.Configurations;

public class CategoriaConfiguration : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> builder)
    {
        builder.ToTable("Categorias");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Nombre).IsRequired().HasMaxLength(Categoria.NombreMaxLength);
        builder.Property(c => c.NombreNormalizado).IsRequired().HasMaxLength(Categoria.NombreMaxLength);
        builder.Property(c => c.Descripcion).HasMaxLength(Categoria.DescripcionMaxLength);
        builder.Property<Guid>(InventoryDbContext.TenantIdPropiedad);

        // La baja es lógica: el nombre solo debe ser único entre las categorías activas del tenant.
        builder.HasIndex(InventoryDbContext.TenantIdPropiedad, nameof(Categoria.NombreNormalizado))
            .IsUnique()
            .HasFilter("\"Activo\"");
    }
}
