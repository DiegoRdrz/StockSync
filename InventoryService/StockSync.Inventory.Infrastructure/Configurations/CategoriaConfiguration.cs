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
        builder.Property(c => c.Descripcion).HasMaxLength(Categoria.DescripcionMaxLength);

        // La baja es lógica: el nombre solo debe ser único entre categorías activas.
        builder.HasIndex(c => c.Nombre).IsUnique().HasFilter("\"Activo\"");
    }
}
