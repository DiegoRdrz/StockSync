using Microsoft.EntityFrameworkCore;
using StockSync.Inventory.Application.Common.Exceptions;
using StockSync.Inventory.Application.Movimientos;
using StockSync.Inventory.Application.Productos;
using StockSync.Inventory.Application.Stocks;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Exceptions;
using StockSync.Inventory.Infrastructure;
using StockSync.Inventory.Infrastructure.Repositories;

namespace StockSync.Inventory.IntegrationTests;

public class StockPostgresTests : PostgresTestBase
{
    private static StockService CrearStockService(InventoryDbContext context) =>
        new(new StockRepository(context), new ProductoRepository(context), new MovimientoStockRepository(context),
            new UnidadDeTrabajo(context));

    private static MovimientoStockService CrearMovimientoService(InventoryDbContext context) =>
        new(new StockRepository(context), new ProductoRepository(context), new MovimientoStockRepository(context),
            new UnidadDeTrabajo(context));

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

    // Con bloqueo optimista solo, la mayoría recibía 409 por concurrencia aunque hubiera stock disponible.
    [PostgreSqlFact]
    public async Task VentasSimultaneas_SeAplicanEnOrdenSinSobreventaNiConflictos()
    {
        var producto = await CrearProductoAsync();
        Guid stockId;
        await using (var context = CrearContexto())
            stockId = (await CrearStockService(context).CrearAsync(new(producto.Id, Guid.NewGuid(), 10), default)).Id;

        var resultados = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(async () =>
        {
            await using var context = CrearContexto();
            try
            {
                await CrearMovimientoService(context).RegistrarSalidaAsync(stockId, new(1), default);
                return true;
            }
            catch (StockInsuficienteException)
            {
                return false;
            }
        })));

        Assert.Equal(10, resultados.Count(vendida => vendida));
        await using var verificacion = CrearContexto();
        Assert.Equal(0, (await verificacion.Stocks.SingleAsync()).Cantidad);
        var salidas = await verificacion.MovimientosStock
            .Where(m => m.Tipo == TipoMovimientoStock.Salida).OrderBy(m => m.CantidadAnterior).ToListAsync();
        Assert.Equal(Enumerable.Range(1, 10), salidas.Select(m => m.CantidadAnterior));
    }

    [PostgreSqlFact]
    public async Task ListadoPorSucursal_ExcluyeProductosDadosDeBajaYPagina()
    {
        var sucursal = Guid.NewGuid();
        var activos = new[] { await CrearProductoAsync("A-1"), await CrearProductoAsync("A-2") };
        var baja = await CrearProductoAsync("B-1");
        await using var context = CrearContexto();
        var stockService = CrearStockService(context);
        foreach (var producto in activos.Append(baja))
            await stockService.CrearAsync(new(producto.Id, sucursal, 0), default);
        await new ProductoService(new ProductoRepository(context), new CategoriaRepository(context))
            .EliminarAsync(baja.Id, default);

        var pagina1 = await stockService.ListarPorSucursalAsync(sucursal, new StockFiltro { TamanoPagina = 1 }, default);
        var pagina2 = await stockService.ListarPorSucursalAsync(sucursal, new StockFiltro { Pagina = 2, TamanoPagina = 1 }, default);

        Assert.Equal(2, pagina1.Total);
        Assert.Equal(activos.Select(p => p.Id),
            pagina1.Items.Concat(pagina2.Items).Select(s => s.ProductoId));
        await Assert.ThrowsAsync<NotFoundException>(
            () => stockService.ListarPorProductoAsync(baja.Id, new StockFiltro(), default));
    }

    [PostgreSqlFact]
    public async Task BajoMinimo_ListaSoloCantidadesMenoresAlMinimoOrdenadasPorFaltante()
    {
        var sucursal = Guid.NewGuid();
        var otraSucursal = Guid.NewGuid();
        var minimoCinco = await CrearProductoAsync("MIN-5", stockMinimo: 5);
        var minimoDiez = await CrearProductoAsync("MIN-10", stockMinimo: 10);
        var sinMinimo = await CrearProductoAsync("MIN-0", stockMinimo: 0);
        await using var context = CrearContexto();
        var stockService = CrearStockService(context);
        var alertaLeve = await stockService.CrearAsync(new(minimoCinco.Id, sucursal, 3), default);
        await stockService.CrearAsync(new(minimoCinco.Id, otraSucursal, 5), default);
        var alertaGrave = await stockService.CrearAsync(new(minimoDiez.Id, otraSucursal, 1), default);
        await stockService.CrearAsync(new(sinMinimo.Id, sucursal, 0), default);

        var todas = await stockService.ListarBajoMinimoAsync(new StockBajoMinimoFiltro(), default);
        var deSucursal = await stockService.ListarBajoMinimoAsync(new StockBajoMinimoFiltro { SucursalId = sucursal }, default);

        Assert.Equal(new[] { alertaGrave.Id, alertaLeve.Id }, todas.Items.Select(a => a.StockId));
        Assert.Equal(new[] { 9, 2 }, todas.Items.Select(a => a.Faltante));
        Assert.Equal("MIN-10", todas.Items[0].ProductoSku);
        Assert.Equal(alertaLeve.Id, Assert.Single(deSucursal.Items).StockId);
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
