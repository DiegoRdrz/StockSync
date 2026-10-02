using Microsoft.EntityFrameworkCore;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // TODO para el equipo: Agregar configuraciones de Fluent API aquí si es necesario
    }
}
