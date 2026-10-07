using Microsoft.EntityFrameworkCore;
using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Infrastructure;

public static class InventoryDbSeeder
{
    public static async Task SeedAsync(InventoryDbContext context, CancellationToken cancellationToken = default)
    {
        var herramientas = await ObtenerCategoriaAsync("Demo Herramientas");
        var papeleria = await ObtenerCategoriaAsync("Demo Papelería");
        var sucursalPrincipal = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var sucursalSecundaria = Guid.Parse("22222222-2222-2222-2222-222222222222");

        await AgregarProductoYStockAsync("Martillo demo", "DEMO-HER-001", 10, 15, 5,
            herramientas.Id, sucursalPrincipal, 0, 10, 3);
        await AgregarProductoYStockAsync("Taladro demo", "DEMO-HER-002", 50, 75, 2,
            herramientas.Id, sucursalPrincipal, 0, 5, 5);
        await AgregarProductoYStockAsync("Cuaderno demo", "DEMO-PAP-001", 2, 4, 3,
            papeleria.Id, sucursalSecundaria, 12, 0, 0);

        // Un único SaveChanges guarda categorías, productos, saldos e historial atómicamente.
        await context.SaveChangesAsync(cancellationToken);

        async Task<Categoria> ObtenerCategoriaAsync(string nombre)
        {
            var normalizado = Categoria.NormalizarNombre(nombre);
            var categoria = await context.Categorias.FirstOrDefaultAsync(
                c => c.Activo && c.NombreNormalizado == normalizado, cancellationToken);
            if (categoria is not null)
                return categoria;

            categoria = Categoria.Crear(nombre, "Categoría para pruebas");
            context.Categorias.Add(categoria);
            return categoria;
        }

        async Task AgregarProductoYStockAsync(string nombre, string sku, decimal compra, decimal venta,
            int minimo, Guid categoriaId, Guid sucursalId, int saldoInicial, int entrada, int salida)
        {
            // Un SKU puede repetirse entre bajas y un activo; se prioriza el activo.
            var producto = await context.Productos
                .OrderByDescending(p => p.Activo)
                .FirstOrDefaultAsync(p => p.Sku == sku, cancellationToken);
            if (producto is null)
            {
                producto = Producto.Crear(nombre, sku, "Producto para pruebas", compra, venta, minimo, categoriaId);
                context.Productos.Add(producto);
            }

            // También se consultan registros inactivos; no se reactivan ni se sobrescriben.
            if (!producto.Activo || await context.Stocks.AnyAsync(
                    s => s.ProductoId == producto.Id && s.SucursalId == sucursalId, cancellationToken))
                return;

            var stock = Stock.Crear(producto.Id, sucursalId, 0);
            context.Stocks.Add(stock);
            if (stock.ActualizarCantidad(saldoInicial) is { } ajusteInicial)
                context.MovimientosStock.Add(ajusteInicial);
            if (entrada > 0)
                context.MovimientosStock.Add(stock.RegistrarMovimiento(TipoMovimientoStock.Entrada, entrada));
            if (salida > 0)
                context.MovimientosStock.Add(stock.RegistrarMovimiento(TipoMovimientoStock.Salida, salida));
        }
    }
}
