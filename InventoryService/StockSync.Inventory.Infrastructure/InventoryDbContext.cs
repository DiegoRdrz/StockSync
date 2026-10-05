using Microsoft.EntityFrameworkCore;
using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Infrastructure;

public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options)
    {
    }

    // TODO para el equipo: Agregar los DbSets de sus entidades aquí
    // Ejemplo:
    // public DbSet<Producto> Productos { get; set; }
    // public DbSet<Categoria> Categorias { get; set; }

    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Categoria> Categorias => Set<Categoria>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InventoryDbContext).Assembly);

        // TODO para el equipo: Agregar configuraciones de Fluent API aquí si es necesario
    }
}
