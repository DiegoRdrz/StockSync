using Microsoft.EntityFrameworkCore;
using StockSync.Inventory.Application.Common;
using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Infrastructure;

public class InventoryDbContext : DbContext
{
    // Propiedad sombra: la multitenencia es un asunto de persistencia y el dominio no la conoce.
    public const string TenantIdPropiedad = "TenantId";

    private readonly ITenantActual? _tenantActual;

    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options)
    {
    }

    public InventoryDbContext(DbContextOptions<InventoryDbContext> options, ITenantActual tenantActual) : base(options)
    {
        _tenantActual = tenantActual;
    }

    // Se evalúa en cada consulta, así que los filtros globales usan el tenant de la solicitud en curso.
    public Guid TenantId => _tenantActual?.TenantId ?? TenantPorDefecto.Id;

    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<MovimientoStock> MovimientosStock => Set<MovimientoStock>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InventoryDbContext).Assembly);

        FiltrarPorTenant<Categoria>(modelBuilder);
        FiltrarPorTenant<Producto>(modelBuilder);
        FiltrarPorTenant<Stock>(modelBuilder);
        FiltrarPorTenant<MovimientoStock>(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AsignarTenantANuevos();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AsignarTenantANuevos();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void FiltrarPorTenant<TEntidad>(ModelBuilder modelBuilder) where TEntidad : class =>
        modelBuilder.Entity<TEntidad>().HasQueryFilter(e => EF.Property<Guid>(e, TenantIdPropiedad) == TenantId);

    private void AsignarTenantANuevos()
    {
        foreach (var entrada in ChangeTracker.Entries().Where(e => e.State == EntityState.Added))
        {
            if (entrada.Metadata.FindProperty(TenantIdPropiedad) is not null)
                entrada.Property(TenantIdPropiedad).CurrentValue = TenantId;
        }
    }
}
