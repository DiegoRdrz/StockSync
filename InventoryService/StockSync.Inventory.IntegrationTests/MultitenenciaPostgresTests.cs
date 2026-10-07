using Microsoft.EntityFrameworkCore;
using StockSync.Inventory.Application.Categorias;
using StockSync.Inventory.Application.Common;
using StockSync.Inventory.Application.Common.Exceptions;
using StockSync.Inventory.Application.Movimientos;
using StockSync.Inventory.Application.Productos;
using StockSync.Inventory.Application.Stocks;
using StockSync.Inventory.Infrastructure;
using StockSync.Inventory.Infrastructure.Repositories;

namespace StockSync.Inventory.IntegrationTests;

public class MultitenenciaPostgresTests : PostgresTestBase
{
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();

    private static ProductoService CrearProductoService(InventoryDbContext context) =>
        new(new ProductoRepository(context), new CategoriaRepository(context));

    private static StockService CrearStockService(InventoryDbContext context) =>
        new(new StockRepository(context), new ProductoRepository(context), new MovimientoStockRepository(context),
            new UnidadDeTrabajo(context));

    private static MovimientoStockService CrearMovimientoService(InventoryDbContext context) =>
        new(new StockRepository(context), new ProductoRepository(context), new MovimientoStockRepository(context),
            new UnidadDeTrabajo(context));

    [PostgreSqlFact]
    public async Task DatosDeUnTenant_NoSonVisiblesNiReferenciablesDesdeOtro()
    {
        Guid categoriaA, productoA, stockA;
        await using (var contextA = CrearContexto(_tenantA))
        {
            categoriaA = (await new CategoriaService(new CategoriaRepository(contextA)).CrearAsync(new("Herramientas", null), default)).Id;
            productoA = (await CrearProductoService(contextA).CrearAsync(new("Martillo", "FER-001", null, 1m, 2m, 5, categoriaA), default)).Id;
            stockA = (await CrearStockService(contextA).CrearAsync(new(productoA, Guid.NewGuid(), 3), default)).Id;
        }

        await using var contextB = CrearContexto(_tenantB);
        var categoriasB = new CategoriaService(new CategoriaRepository(contextB));
        var productosB = CrearProductoService(contextB);
        var stocksB = CrearStockService(contextB);

        await Assert.ThrowsAsync<NotFoundException>(() => categoriasB.ObtenerPorIdAsync(categoriaA, default));
        await Assert.ThrowsAsync<NotFoundException>(() => productosB.ObtenerPorIdAsync(productoA, default));
        await Assert.ThrowsAsync<NotFoundException>(() => stocksB.ObtenerPorIdAsync(stockA, default));
        await Assert.ThrowsAsync<NotFoundException>(() => CrearMovimientoService(contextB).RegistrarSalidaAsync(stockA, new(1), default));
        Assert.Equal(0, (await productosB.ListarAsync(new ProductoFiltro(), default)).Total);
        Assert.Equal(0, (await stocksB.ListarBajoMinimoAsync(new StockBajoMinimoFiltro(), default)).Total);
        Assert.Equal(0, (await CrearMovimientoService(contextB).ListarGeneralAsync(1, default)).Total);
        await Assert.ThrowsAsync<ValidationException>(() =>
            productosB.CrearAsync(new("Otro", "FER-002", null, 1m, 2m, 0, categoriaA), default));
        await Assert.ThrowsAsync<ValidationException>(() =>
            stocksB.CrearAsync(new(productoA, Guid.NewGuid(), 1), default));

        var categoriaB = await categoriasB.CrearAsync(new("Herramientas", null), default);
        await productosB.CrearAsync(new("Martillo", "FER-001", null, 1m, 2m, 0, categoriaB.Id), default);

        await using var verificacion = CrearContexto(_tenantA);
        Assert.Equal(1, await verificacion.Productos.CountAsync());
        var tenantsPorSku = await verificacion.Productos.IgnoreQueryFilters()
            .Where(p => p.Sku == "FER-001")
            .Select(p => EF.Property<Guid>(p, InventoryDbContext.TenantIdPropiedad))
            .ToListAsync();
        Assert.Equal(new[] { _tenantA, _tenantB }.Order(), tenantsPorSku.Order());
        Assert.Equal(_tenantA, await verificacion.MovimientosStock
            .Select(m => EF.Property<Guid>(m, InventoryDbContext.TenantIdPropiedad)).SingleAsync());
    }

    [PostgreSqlFact]
    public async Task ContextoSinTenant_UsaElTenantPorDefecto()
    {
        await using (var sinTenant = CrearContexto())
            await new CategoriaService(new CategoriaRepository(sinTenant)).CrearAsync(new("Demo", null), default);

        await using var porDefecto = CrearContexto(TenantPorDefecto.Id);
        await using var otro = CrearContexto(_tenantA);
        Assert.Equal(1, await porDefecto.Categorias.CountAsync());
        Assert.Equal(0, await otro.Categorias.CountAsync());
    }
}
