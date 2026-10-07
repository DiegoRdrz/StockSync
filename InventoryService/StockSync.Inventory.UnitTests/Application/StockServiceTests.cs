using StockSync.Inventory.Application.Common.Exceptions;
using StockSync.Inventory.Application.Stocks;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Repositories;

namespace StockSync.Inventory.UnitTests.Application;

public partial class StockServiceTests
{
    private readonly FakeStockRepository _stockRepository = new();
    private readonly FakeProductoRepository _productoRepository = new();
    private readonly UnidadDeTrabajoEnMemoria _unidadDeTrabajo = new();
    private readonly StockService _service;

    public StockServiceTests()
    {
        _service = new StockService(_stockRepository, _productoRepository, _movimientos, _unidadDeTrabajo);
    }

    private Producto CrearProducto(string sku = "FER-001")
    {
        var producto = Producto.Crear("Martillo", sku, null, 10m, 15m, 5, null);
        _productoRepository.Productos.Add(producto);
        return producto;
    }

    [Fact]
    public async Task CrearAsync_RequestValido_GuardaYDevuelveStock()
    {
        var producto = CrearProducto();
        var sucursalId = Guid.NewGuid();

        var response = await _service.CrearAsync(new StockRequest(producto.Id, sucursalId, 20), CancellationToken.None);

        Assert.Equal(producto.Id, response.ProductoId);
        Assert.Equal(sucursalId, response.SucursalId);
        Assert.Equal(20, response.Cantidad);
        Assert.Single(_stockRepository.Stocks);
        Assert.Equal(1, _stockRepository.Guardados);
        var saldoInicial = Assert.Single(_movimientos.Items);
        Assert.Equal(TipoMovimientoStock.Ajuste, saldoInicial.Tipo);
        Assert.Equal((0, 20), (saldoInicial.CantidadAnterior, saldoInicial.CantidadPosterior));
    }

    [Fact]
    public async Task CrearAsync_SaldoInicialCero_NoRegistraMovimiento()
    {
        var producto = CrearProducto();

        await _service.CrearAsync(new StockRequest(producto.Id, Guid.NewGuid(), 0), CancellationToken.None);

        Assert.Empty(_movimientos.Items);
    }

    [Fact]
    public async Task CrearAsync_ProductoInexistente_LanzaValidationExceptionSinGuardar()
    {
        var request = new StockRequest(Guid.NewGuid(), Guid.NewGuid(), 20);

        await Assert.ThrowsAsync<ValidationException>(() => _service.CrearAsync(request, CancellationToken.None));
        Assert.Empty(_stockRepository.Stocks);
    }

    [Fact]
    public async Task CrearAsync_ProductoDadoDeBaja_LanzaValidationException()
    {
        var producto = CrearProducto();
        producto.Desactivar();

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.CrearAsync(new StockRequest(producto.Id, Guid.NewGuid(), 20), CancellationToken.None));
    }

    [Fact]
    public async Task CrearAsync_AsignacionDuplicada_LanzaConflictException()
    {
        var producto = CrearProducto();
        var sucursalId = Guid.NewGuid();
        await _service.CrearAsync(new StockRequest(producto.Id, sucursalId, 20), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(
            () => _service.CrearAsync(new StockRequest(producto.Id, sucursalId, 5), CancellationToken.None));
        Assert.Single(_stockRepository.Stocks);
    }

    [Fact]
    public async Task CrearAsync_MismoProductoEnOtraSucursal_EsValido()
    {
        var producto = CrearProducto();
        await _service.CrearAsync(new StockRequest(producto.Id, Guid.NewGuid(), 20), CancellationToken.None);

        await _service.CrearAsync(new StockRequest(producto.Id, Guid.NewGuid(), 8), CancellationToken.None);

        Assert.Equal(2, _stockRepository.Stocks.Count);
    }

    [Fact]
    public async Task CrearAsync_RequestInvalido_LanzaValidationExceptionSinGuardar()
    {
        var producto = CrearProducto();
        var request = new StockRequest(producto.Id, Guid.Empty, -1);

        await Assert.ThrowsAsync<ValidationException>(() => _service.CrearAsync(request, CancellationToken.None));
        Assert.Empty(_stockRepository.Stocks);
        Assert.Equal(0, _stockRepository.Guardados);
    }

    [Fact]
    public async Task ObtenerPorIdAsync_Existente_DevuelveStock()
    {
        var producto = CrearProducto();
        var creado = await _service.CrearAsync(new StockRequest(producto.Id, Guid.NewGuid(), 20), CancellationToken.None);

        var response = await _service.ObtenerPorIdAsync(creado.Id, CancellationToken.None);

        Assert.Equal(creado, response);
    }

    [Fact]
    public async Task ObtenerPorIdAsync_Inexistente_LanzaNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.ObtenerPorIdAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task ListarPorSucursalAsync_DevuelveSoloLasAsignacionesDeLaSucursal()
    {
        var producto1 = CrearProducto("FER-001");
        var producto2 = CrearProducto("FER-002");
        var sucursalA = Guid.NewGuid();
        var sucursalB = Guid.NewGuid();
        await _service.CrearAsync(new StockRequest(producto1.Id, sucursalA, 10), CancellationToken.None);
        await _service.CrearAsync(new StockRequest(producto2.Id, sucursalA, 25), CancellationToken.None);
        await _service.CrearAsync(new StockRequest(producto1.Id, sucursalB, 4), CancellationToken.None);

        var resultado = await _service.ListarPorSucursalAsync(sucursalA, new StockFiltro(), CancellationToken.None);

        Assert.Equal(2, resultado.Total);
        Assert.All(resultado.Items, s => Assert.Equal(sucursalA, s.SucursalId));
    }

    [Fact]
    public async Task ListarPorSucursalAsync_SinAsignaciones_DevuelveListaVacia()
    {
        var resultado = await _service.ListarPorSucursalAsync(Guid.NewGuid(), new StockFiltro(), CancellationToken.None);

        Assert.Empty(resultado.Items);
    }

    [Fact]
    public async Task ListarPorSucursalAsync_Pagina()
    {
        var sucursalId = Guid.NewGuid();
        for (var i = 0; i < 3; i++)
            await _service.CrearAsync(new StockRequest(CrearProducto($"FER-00{i}").Id, sucursalId, 0), CancellationToken.None);

        var resultado = await _service.ListarPorSucursalAsync(
            sucursalId, new StockFiltro { Pagina = 2, TamanoPagina = 2 }, CancellationToken.None);

        Assert.Single(resultado.Items);
        Assert.Equal(3, resultado.Total);
        Assert.Equal(2, resultado.TotalPaginas);
        await Assert.ThrowsAsync<ValidationException>(() => _service.ListarPorSucursalAsync(
            sucursalId, new StockFiltro { Pagina = int.MaxValue }, CancellationToken.None));
    }

    [Fact]
    public async Task ListarPorProductoAsync_DevuelveSoloLasAsignacionesDelProducto()
    {
        var producto1 = CrearProducto("FER-001");
        var producto2 = CrearProducto("FER-002");
        await _service.CrearAsync(new StockRequest(producto1.Id, Guid.NewGuid(), 10), CancellationToken.None);
        await _service.CrearAsync(new StockRequest(producto1.Id, Guid.NewGuid(), 20), CancellationToken.None);
        await _service.CrearAsync(new StockRequest(producto2.Id, Guid.NewGuid(), 5), CancellationToken.None);

        var resultado = await _service.ListarPorProductoAsync(producto1.Id, new StockFiltro(), CancellationToken.None);

        Assert.Equal(2, resultado.Total);
        Assert.All(resultado.Items, s => Assert.Equal(producto1.Id, s.ProductoId));
    }

    [Fact]
    public async Task ListarPorProductoAsync_ProductoInexistenteODadoDeBaja_LanzaNotFoundException()
    {
        var baja = CrearProducto();
        baja.Desactivar();

        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.ListarPorProductoAsync(Guid.NewGuid(), new StockFiltro(), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.ListarPorProductoAsync(baja.Id, new StockFiltro(), CancellationToken.None));
    }

    [Fact]
    public async Task ActualizarCantidadAsync_CantidadValida_ModificaSoloLaCantidad()
    {
        var producto = CrearProducto();
        var sucursalId = Guid.NewGuid();
        var creado = await _service.CrearAsync(new StockRequest(producto.Id, sucursalId, 20), CancellationToken.None);

        var actualizado = await _service.ActualizarCantidadAsync(
            creado.Id,
            new StockCantidadRequest(3),
            CancellationToken.None);

        Assert.Equal(3, actualizado.Cantidad);
        Assert.Equal(creado.Id, actualizado.Id);
        Assert.Equal(producto.Id, actualizado.ProductoId);
        Assert.Equal(sucursalId, actualizado.SucursalId);
        Assert.Equal(2, _stockRepository.Guardados);
        var ajuste = _movimientos.Items.Last();
        Assert.Equal(TipoMovimientoStock.Ajuste, ajuste.Tipo);
        Assert.Equal((20, 3, 17), (ajuste.CantidadAnterior, ajuste.CantidadPosterior, ajuste.Cantidad));
    }

    [Fact]
    public async Task ActualizarCantidadAsync_CantidadEnCero_EsValido()
    {
        var producto = CrearProducto();
        var creado = await _service.CrearAsync(new StockRequest(producto.Id, Guid.NewGuid(), 20), CancellationToken.None);

        var actualizado = await _service.ActualizarCantidadAsync(creado.Id, new StockCantidadRequest(0), CancellationToken.None);

        Assert.Equal(0, actualizado.Cantidad);
    }

    [Fact]
    public async Task ActualizarCantidadAsync_CantidadNegativa_LanzaValidationExceptionSinModificar()
    {
        var producto = CrearProducto();
        var creado = await _service.CrearAsync(new StockRequest(producto.Id, Guid.NewGuid(), 20), CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.ActualizarCantidadAsync(creado.Id, new StockCantidadRequest(-1), CancellationToken.None));
        Assert.Equal(20, _stockRepository.Stocks.Single().Cantidad);
    }

    [Fact]
    public async Task ActualizarCantidadAsync_Inexistente_LanzaNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.ActualizarCantidadAsync(Guid.NewGuid(), new StockCantidadRequest(5), CancellationToken.None));
    }

    [Fact]
    public async Task EliminarAsync_EliminaLaAsignacionPeroNoElProducto()
    {
        var producto = CrearProducto();
        var creado = await _service.CrearAsync(new StockRequest(producto.Id, Guid.NewGuid(), 0), CancellationToken.None);

        await _service.EliminarAsync(creado.Id, CancellationToken.None);

        Assert.Empty(_stockRepository.Stocks);
        Assert.True(producto.Activo);
        Assert.Single(_productoRepository.Productos);
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.ObtenerPorIdAsync(creado.Id, CancellationToken.None));
    }

    [Fact]
    public async Task EliminarAsync_ConExistencias_LanzaConflictExceptionSinEliminar()
    {
        var producto = CrearProducto();
        var creado = await _service.CrearAsync(new StockRequest(producto.Id, Guid.NewGuid(), 5), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => _service.EliminarAsync(creado.Id, CancellationToken.None));
        Assert.Single(_stockRepository.Stocks);
    }

    [Fact]
    public async Task ActualizarCantidadAsync_ProductoDadoDeBaja_LanzaConflictExceptionSinModificar()
    {
        var producto = CrearProducto();
        var creado = await _service.CrearAsync(new StockRequest(producto.Id, Guid.NewGuid(), 5), CancellationToken.None);
        producto.Desactivar();

        await Assert.ThrowsAsync<ConflictException>(
            () => _service.ActualizarCantidadAsync(creado.Id, new StockCantidadRequest(0), CancellationToken.None));
        Assert.Equal(5, _stockRepository.Stocks.Single().Cantidad);
        Assert.Single(_movimientos.Items);
    }

    [Fact]
    public async Task EliminarAsync_Inexistente_LanzaNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.EliminarAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task CrearAsync_TrasEliminarLaAsignacion_PermiteVolverAAsignar()
    {
        var producto = CrearProducto();
        var sucursalId = Guid.NewGuid();
        var creado = await _service.CrearAsync(new StockRequest(producto.Id, sucursalId, 0), CancellationToken.None);
        await _service.EliminarAsync(creado.Id, CancellationToken.None);

        var nuevo = await _service.CrearAsync(new StockRequest(producto.Id, sucursalId, 7), CancellationToken.None);

        Assert.Equal(7, nuevo.Cantidad);
    }

    private sealed class FakeStockRepository : IStockRepository
    {
        public List<Stock> Stocks { get; } = [];
        public int Guardados { get; private set; }

        public Task<Stock?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Stocks.FirstOrDefault(s => s.Id == id));

        public Task<Stock?> ObtenerParaActualizarAsync(Guid id, CancellationToken cancellationToken) =>
            ObtenerPorIdAsync(id, cancellationToken);

        public Task<Stock?> ObtenerPorProductoYSucursalAsync(Guid productoId, Guid sucursalId, CancellationToken cancellationToken) =>
            Task.FromResult(Stocks.FirstOrDefault(s => s.ProductoId == productoId && s.SucursalId == sucursalId));

        public Task<(IReadOnlyList<Stock> Items, int Total)> ListarPorSucursalAsync(
            Guid sucursalId, int skip, int take, CancellationToken cancellationToken) =>
            Paginar(Stocks.Where(s => s.SucursalId == sucursalId).ToList(), skip, take);

        public Task<(IReadOnlyList<Stock> Items, int Total)> ListarPorProductoAsync(
            Guid productoId, int skip, int take, CancellationToken cancellationToken) =>
            Paginar(Stocks.Where(s => s.ProductoId == productoId).ToList(), skip, take);

        // La comparación con el stock mínimo necesita el join con productos: se prueba contra PostgreSQL.
        public Task<(IReadOnlyList<StockBajoMinimoDetalle> Items, int Total)> ListarBajoMinimoAsync(
            Guid? sucursalId, int skip, int take, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        private static Task<(IReadOnlyList<Stock> Items, int Total)> Paginar(List<Stock> stocks, int skip, int take) =>
            Task.FromResult<(IReadOnlyList<Stock>, int)>((stocks.Skip(skip).Take(take).ToList(), stocks.Count));

        public Task<bool> ExisteAsync(Guid productoId, Guid sucursalId, CancellationToken cancellationToken) =>
            Task.FromResult(Stocks.Any(s => s.ProductoId == productoId && s.SucursalId == sucursalId));

        public Task AgregarAsync(Stock stock, CancellationToken cancellationToken)
        {
            Stocks.Add(stock);
            return Task.CompletedTask;
        }

        public void Eliminar(Stock stock) => Stocks.Remove(stock);

        public Task GuardarCambiosAsync(CancellationToken cancellationToken)
        {
            Guardados++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProductoRepository : IProductoRepository
    {
        public List<Producto> Productos { get; } = [];

        public Task<Producto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Productos.FirstOrDefault(p => p.Id == id && p.Activo));

        public Task<Producto?> ObtenerParaActualizarAsync(Guid id, CancellationToken cancellationToken) =>
            ObtenerPorIdAsync(id, cancellationToken);

        public Task<(IReadOnlyList<Producto> Items, int Total)> ListarAsync(
            string? nombre,
            string? sku,
            Guid? categoriaId,
            int skip,
            int take,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> ExisteSkuAsync(string sku, Guid? excluirId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> TieneExistenciasAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AgregarAsync(Producto producto, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task GuardarCambiosAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
