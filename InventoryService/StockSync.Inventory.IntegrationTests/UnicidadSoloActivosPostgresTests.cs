using Microsoft.EntityFrameworkCore;
using StockSync.Inventory.Application.Categorias;
using StockSync.Inventory.Application.Common.Exceptions;
using StockSync.Inventory.Application.Productos;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Infrastructure;
using StockSync.Inventory.Infrastructure.Repositories;

namespace StockSync.Inventory.IntegrationTests;

public class UnicidadSoloActivosPostgresTests : PostgresTestBase
{
    [PostgreSqlFact]
    public async Task Categoria_RecrearYRenombrarConNombreDeBaja_SePermite()
    {
        await using var context = CrearContexto();
        var service = new CategoriaService(new CategoriaRepository(context));
        var original = await service.CrearAsync(new("Chocodsa", null), default);
        await service.EliminarAsync(original.Id, default);

        var recreada = await service.CrearAsync(new("Chocodsa", null), default);
        var otra = await service.CrearAsync(new("Temporal", null), default);
        await service.EliminarAsync(otra.Id, default);
        var renombrada = await service.ActualizarAsync(recreada.Id, new("Temporal", null), default);

        Assert.NotEqual(original.Id, recreada.Id);
        Assert.Equal("Temporal", renombrada.Nombre);
        await Assert.ThrowsAsync<ConflictException>(() => service.CrearAsync(new(" temporal ", null), default));
    }

    [PostgreSqlFact]
    public async Task Producto_ReutilizarSkuDeBaja_SePermite()
    {
        await using var context = CrearContexto();
        var service = new ProductoService(new ProductoRepository(context), new CategoriaRepository(context));
        ProductoRequest Request(string sku) => new("Martillo", sku, null, 10m, 15m, 0, null);
        var original = await service.CrearAsync(Request("FER-001"), default);
        await service.EliminarAsync(original.Id, default);

        var nuevo = await service.CrearAsync(Request("fer-001"), default);
        var otro = await service.CrearAsync(Request("FER-002"), default);
        await service.EliminarAsync(otro.Id, default);
        var actualizado = await service.ActualizarAsync(nuevo.Id, Request("FER-002"), default);

        Assert.NotEqual(original.Id, nuevo.Id);
        Assert.Equal("FER-002", actualizado.Sku);
        await Assert.ThrowsAsync<ConflictException>(() => service.CrearAsync(Request("FER-002"), default));
    }

    // Sin la verificación previa del servicio, el índice filtrado debe seguir impidiendo dos activos iguales.
    [PostgreSqlFact]
    public async Task IndicesUnicos_RechazanDuplicadosActivos()
    {
        await using (var context = CrearContexto())
        {
            var categoriaBaja = Categoria.Crear("Chocodsa", null);
            categoriaBaja.Desactivar();
            var productoBaja = Producto.Crear("Viejo", "SKU-1", null, 1m, 2m, 0, null);
            productoBaja.Desactivar();
            context.AddRange(categoriaBaja, Categoria.Crear("Chocodsa", null),
                productoBaja, Producto.Crear("Nuevo", "SKU-1", null, 1m, 2m, 0, null));
            await context.SaveChangesAsync();
        }

        await using (var context = CrearContexto())
        {
            var repo = new CategoriaRepository(context);
            await repo.AgregarAsync(Categoria.Crear(" CHOCODSA ", null), default);
            await Assert.ThrowsAsync<ConflictException>(() => repo.GuardarCambiosAsync(default));
        }

        await using (var context = CrearContexto())
        {
            var repo = new ProductoRepository(context);
            await repo.AgregarAsync(Producto.Crear("Otro", "SKU-1", null, 1m, 2m, 0, null), default);
            await Assert.ThrowsAsync<ConflictException>(() => repo.GuardarCambiosAsync(default));
        }
    }

    [PostgreSqlFact]
    public async Task Semilla_ConSkuReutilizadoYCategoriaDeBaja_NoFallaNiUsaInactivos()
    {
        await using (var context = CrearContexto())
        {
            var categoriaBaja = Categoria.Crear("Demo Herramientas", null);
            categoriaBaja.Desactivar();
            var martilloBaja = Producto.Crear("Martillo viejo", "DEMO-HER-001", null, 1m, 2m, 0, null);
            martilloBaja.Desactivar();
            context.AddRange(categoriaBaja, martilloBaja,
                Producto.Crear("Martillo nuevo", "DEMO-HER-001", null, 1m, 2m, 0, null));
            await context.SaveChangesAsync();
        }

        await using (var context = CrearContexto())
        {
            await InventoryDbSeeder.SeedAsync(context);
            await InventoryDbSeeder.SeedAsync(context);
        }

        await using var verificacion = CrearContexto();
        var categoriasDeProductos = await verificacion.Productos
            .Where(p => p.CategoriaId != null)
            .Join(verificacion.Categorias, p => p.CategoriaId, c => c.Id, (p, c) => c.Activo)
            .ToListAsync();
        Assert.NotEmpty(categoriasDeProductos);
        Assert.All(categoriasDeProductos, Assert.True);
        var martilloActivo = await verificacion.Productos.SingleAsync(p => p.Sku == "DEMO-HER-001" && p.Activo);
        Assert.Equal("Martillo nuevo", martilloActivo.Nombre);
        Assert.Equal(1, await verificacion.Stocks.CountAsync(s => s.ProductoId == martilloActivo.Id));
        Assert.Equal(2, await verificacion.Productos.CountAsync(p => p.Sku == "DEMO-HER-001"));
    }
}
