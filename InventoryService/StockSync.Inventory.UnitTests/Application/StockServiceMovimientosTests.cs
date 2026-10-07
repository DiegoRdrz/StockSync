using StockSync.Inventory.Application.Common.Exceptions;
using StockSync.Inventory.Application.Movimientos;
using StockSync.Inventory.Application.Stocks;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Exceptions;
using StockSync.Inventory.Domain.Repositories;

namespace StockSync.Inventory.UnitTests.Application;

// Reutiliza los repositorios en memoria y la creación de productos de StockServiceTests.
public partial class StockServiceTests
{
    private readonly FakeMovimientoRepository _movimientos = new();
    private MovimientoStockService CrearServicioMovimientos() => new(_stockRepository, _productoRepository, _movimientos, _unidadDeTrabajo);

    [Fact]
    public async Task Movimientos_EntradaYSalida_GuardanSaldoEHistorial()
    {
        var producto = CrearProducto();
        var stock = await _service.CrearAsync(new StockRequest(producto.Id, Guid.NewGuid(), 0), default);
        var service = CrearServicioMovimientos();

        var entrada = await service.RegistrarEntradaAsync(stock.Id, new(5), default);
        var salida = await service.RegistrarSalidaAsync(stock.Id, new(5), default);

        Assert.Equal("Entrada", entrada.Tipo);
        Assert.Equal("Salida", salida.Tipo);
        Assert.Equal(0, (await _service.ObtenerPorIdAsync(stock.Id, default)).Cantidad);
        Assert.Equal(2, _movimientos.Items.Count);
        Assert.Equal(3, _stockRepository.Guardados);
        Assert.Equal(salida, await service.ObtenerPorIdAsync(stock.Id, salida.Id, default));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(6)]
    public async Task Movimientos_SalidaRechazada_NoGuardaNiGeneraHistorial(int cantidad)
    {
        var producto = CrearProducto();
        var stock = await _service.CrearAsync(new StockRequest(producto.Id, Guid.NewGuid(), 5), default);
        var service = CrearServicioMovimientos();

        if (cantidad <= 0)
            await Assert.ThrowsAsync<ValidationException>(() => service.RegistrarSalidaAsync(stock.Id, new(cantidad), default));
        else
            await Assert.ThrowsAsync<StockInsuficienteException>(() => service.RegistrarSalidaAsync(stock.Id, new(cantidad), default));

        Assert.Equal(5, (await _service.ObtenerPorIdAsync(stock.Id, default)).Cantidad);
        Assert.Equal(TipoMovimientoStock.Ajuste, Assert.Single(_movimientos.Items).Tipo);
        Assert.Equal(1, _stockRepository.Guardados);
    }

    [Fact]
    public async Task Movimientos_StockInexistente_DevuelveNotFound()
    {
        var service = CrearServicioMovimientos();
        await Assert.ThrowsAsync<NotFoundException>(() => service.RegistrarEntradaAsync(Guid.NewGuid(), new(1), default));
        Assert.Empty(_movimientos.Items);
        Assert.Equal(0, _stockRepository.Guardados);
    }

    [Fact]
    public async Task Movimientos_ProductoInactivo_NoModificaStock()
    {
        var producto = CrearProducto();
        var stock = await _service.CrearAsync(new StockRequest(producto.Id, Guid.NewGuid(), 5), default);
        producto.Desactivar();

        await Assert.ThrowsAsync<ConflictException>(() => CrearServicioMovimientos().RegistrarSalidaAsync(stock.Id, new(1), default));
        Assert.Equal(5, (await _service.ObtenerPorIdAsync(stock.Id, default)).Cantidad);
        Assert.Equal(TipoMovimientoStock.Ajuste, Assert.Single(_movimientos.Items).Tipo);
    }

    [Fact]
    public async Task Movimientos_Historial_RespetaStockYPaginacion()
    {
        var producto = CrearProducto();
        var stock = await _service.CrearAsync(new StockRequest(producto.Id, Guid.NewGuid(), 0), default);
        var otro = await _service.CrearAsync(new StockRequest(producto.Id, Guid.NewGuid(), 0), default);
        var service = CrearServicioMovimientos();
        var primero = await service.RegistrarEntradaAsync(stock.Id, new(5), default);
        await service.RegistrarSalidaAsync(stock.Id, new(2), default);
        await service.RegistrarEntradaAsync(otro.Id, new(9), default);

        var pagina = await service.ListarAsync(stock.Id, new(2, 1), default);

        Assert.Equal(2, pagina.Total);
        Assert.Equal(2, pagina.TotalPaginas);
        Assert.Equal(primero, Assert.Single(pagina.Items));
        await Assert.ThrowsAsync<NotFoundException>(() => service.ObtenerPorIdAsync(otro.Id, primero.Id, default));
        await Assert.ThrowsAsync<ValidationException>(() => service.ListarAsync(stock.Id, new(0, 20), default));
        await Assert.ThrowsAsync<NotFoundException>(() => service.ListarAsync(Guid.NewGuid(), new(), default));
    }

    private sealed class FakeMovimientoRepository : IMovimientoStockRepository
    {
        public Task<(IReadOnlyList<MovimientoStockDetalle> Items, int Total)> ListarGeneralAsync(
            int skip, int take, CancellationToken cancellationToken) =>
            throw new NotSupportedException("El listado con productos se prueba contra PostgreSQL en integración.");

        public List<MovimientoStock> Items { get; } = [];

        public Task AgregarAsync(MovimientoStock movimiento, CancellationToken cancellationToken)
        {
            Items.Add(movimiento);
            return Task.CompletedTask;
        }

        public Task<MovimientoStock?> ObtenerPorIdAsync(Guid stockId, Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Items.FirstOrDefault(m => m.StockId == stockId && m.Id == id));

        public Task<(IReadOnlyList<MovimientoStock> Items, int Total)> ListarAsync(Guid stockId, int skip, int take, CancellationToken cancellationToken)
        {
            var filtrados = Items.Where(m => m.StockId == stockId).OrderByDescending(m => m.Fecha).ThenByDescending(m => m.Id).ToList();
            return Task.FromResult<(IReadOnlyList<MovimientoStock>, int)>((filtrados.Skip(skip).Take(take).ToList(), filtrados.Count));
        }
    }
}
