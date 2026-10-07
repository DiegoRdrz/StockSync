using Microsoft.EntityFrameworkCore;
using StockSync.Inventory.Application.Common.Exceptions;
using StockSync.Inventory.Application.Movimientos;
using StockSync.Inventory.Application.Productos;
using StockSync.Inventory.Application.Stocks;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Infrastructure;
using StockSync.Inventory.Infrastructure.Repositories;

namespace StockSync.Inventory.IntegrationTests;

public class StockPostgresTests : PostgresTestBase
{
    private static StockService CrearStockService(InventoryDbContext context) =>
        new(new StockRepository(context), new ProductoRepository(context), new MovimientoStockRepository(context));

    private static MovimientoStockService CrearMovimientoService(InventoryDbContext context) =>
        new(new StockRepository(context), new ProductoRepository(context), new MovimientoStockRepository(context));

    private async Task<Producto> CrearProductoAsync(string sku = "FER-001", int stockMinimo = 0)
    {
        await using var context = CrearContexto();
        var producto = Producto.Crear($"Producto {sku}", sku, null, 1m, 2m, stockMinimo, null);
        context.Productos.Add(producto);
        await context.SaveChangesAsync();
        return producto;
    }

    [PostgreSqlFact]
    public async Task SaldoInicialYAjustes_QuedanEnElHistorialYCuadranConElSaldo()
    {
        var producto = await CrearProductoAsync();
        await using var context = CrearContexto();
        var stockService = CrearStockService(context);

        var stock = await stockService.CrearAsync(new(producto.Id, Guid.NewGuid(), 10), default);
        await CrearMovimientoService(context).RegistrarSalidaAsync(stock.Id, new(3), default);
        await stockService.ActualizarCantidadAsync(stock.Id, new(20), default);
        await stockService.ActualizarCantidadAsync(stock.Id, new(20), default);

        await using var verificacion = CrearContexto();
        var movimientos = await verificacion.MovimientosStock
            .Where(m => m.StockId == stock.Id).OrderBy(m => m.Fecha).ToListAsync();
        var saldo = (await verificacion.Stocks.SingleAsync(s => s.Id == stock.Id)).Cantidad;
        Assert.Equal(
            new[] { TipoMovimientoStock.Ajuste, TipoMovimientoStock.Salida, TipoMovimientoStock.Ajuste },
            movimientos.Select(m => m.Tipo));
        Assert.Equal(20, saldo);
        Assert.Equal(saldo, movimientos.Sum(m => m.CantidadPosterior - m.CantidadAnterior));
    }

    [PostgreSqlFact]
    public async Task EliminarProductoYStock_SoloSinExistencias()
    {
        var producto = await CrearProductoAsync();
        await using var context = CrearContexto();
        var productoService = new ProductoService(new ProductoRepository(context), new CategoriaRepository(context));
        var stockService = CrearStockService(context);
        var conExistencias = await stockService.CrearAsync(new(producto.Id, Guid.NewGuid(), 5), default);
        var sinExistencias = await stockService.CrearAsync(new(producto.Id, Guid.NewGuid(), 0), default);

        await Assert.ThrowsAsync<ConflictException>(() => productoService.EliminarAsync(producto.Id, default));
        await Assert.ThrowsAsync<ConflictException>(() => stockService.EliminarAsync(conExistencias.Id, default));
        await stockService.EliminarAsync(sinExistencias.Id, default);
        await CrearMovimientoService(context).RegistrarSalidaAsync(conExistencias.Id, new(5), default);
        await productoService.EliminarAsync(producto.Id, default);

        await using var verificacion = CrearContexto();
        Assert.False((await verificacion.Productos.SingleAsync()).Activo);
        Assert.Equal(conExistencias.Id, (await verificacion.Stocks.SingleAsync()).Id);
    }
}
