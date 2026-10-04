using StockSync.Inventory.Application.Common.Exceptions;
using StockSync.Inventory.Application.Productos;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Repositories;

namespace StockSync.Inventory.UnitTests.Application;

public class ProductoServiceTests
{
    private readonly FakeProductoRepository _repository = new();
    private readonly ProductoService _service;

    public ProductoServiceTests()
    {
        _service = new ProductoService(_repository);
    }

    private static ProductoRequest Request(string sku = "FER-001") =>
        new("Martillo", sku, null, 10m, 15m, 5, null);

    [Fact]
    public async Task CrearAsync_RequestValido_GuardaYDevuelveProducto()
    {
        var response = await _service.CrearAsync(Request("fer-001"), CancellationToken.None);

        Assert.Equal("FER-001", response.Sku);
        Assert.Single(_repository.Productos);
        Assert.Equal(1, _repository.Guardados);
    }

    [Fact]
    public async Task CrearAsync_SkuDuplicado_LanzaConflictException()
    {
        await _service.CrearAsync(Request("FER-001"), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(
            () => _service.CrearAsync(Request(" fer-001 "), CancellationToken.None));
    }

    [Fact]
    public async Task CrearAsync_RequestInvalido_LanzaValidationExceptionSinGuardar()
    {
        var request = Request() with { PrecioVenta = -1m };

        await Assert.ThrowsAsync<ValidationException>(() => _service.CrearAsync(request, CancellationToken.None));
        Assert.Empty(_repository.Productos);
    }

    [Fact]
    public async Task ObtenerPorIdAsync_Inexistente_LanzaNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.ObtenerPorIdAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task ActualizarAsync_MismoSkuDelPropioProducto_EsValido()
    {
        var creado = await _service.CrearAsync(Request("FER-001"), CancellationToken.None);

        var actualizado = await _service.ActualizarAsync(
            creado.Id,
            Request("FER-001") with { Nombre = "Martillo grande" },
            CancellationToken.None);

        Assert.Equal("Martillo grande", actualizado.Nombre);
    }

    [Fact]
    public async Task ActualizarAsync_SkuDeOtroProducto_LanzaConflictException()
    {
        await _service.CrearAsync(Request("FER-001"), CancellationToken.None);
        var otro = await _service.CrearAsync(Request("FER-002"), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(
            () => _service.ActualizarAsync(otro.Id, Request("FER-001"), CancellationToken.None));
    }

    [Fact]
    public async Task EliminarAsync_DesactivaYLuegoNoSeEncuentra()
    {
        var creado = await _service.CrearAsync(Request(), CancellationToken.None);

        await _service.EliminarAsync(creado.Id, CancellationToken.None);

        Assert.False(_repository.Productos.Single().Activo);
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.ObtenerPorIdAsync(creado.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.EliminarAsync(creado.Id, CancellationToken.None));
    }

    [Fact]
    public async Task ListarAsync_FiltroInvalido_LanzaValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _service.ListarAsync(new ProductoFiltro { Pagina = 0 }, CancellationToken.None));
    }

    [Fact]
    public async Task ListarAsync_CalculaPaginacion()
    {
        for (var i = 0; i < 5; i++)
            await _service.CrearAsync(Request($"FER-00{i}"), CancellationToken.None);

        var resultado = await _service.ListarAsync(
            new ProductoFiltro { Pagina = 2, TamanoPagina = 2 },
            CancellationToken.None);

        Assert.Equal(2, resultado.Items.Count);
        Assert.Equal(5, resultado.Total);
        Assert.Equal(3, resultado.TotalPaginas);
    }

    private sealed class FakeProductoRepository : IProductoRepository
    {
        public List<Producto> Productos { get; } = [];
        public int Guardados { get; private set; }

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
            CancellationToken cancellationToken)
        {
            var activos = Productos.Where(p => p.Activo).ToList();
            IReadOnlyList<Producto> items = activos.Skip(skip).Take(take).ToList();
            return Task.FromResult((items, activos.Count));
        }

        public Task<bool> ExisteSkuAsync(string sku, Guid? excluirId, CancellationToken cancellationToken) =>
            Task.FromResult(Productos.Any(p => p.Sku == sku && p.Id != excluirId));

        public Task AgregarAsync(Producto producto, CancellationToken cancellationToken)
        {
            Productos.Add(producto);
            return Task.CompletedTask;
        }

        public Task GuardarCambiosAsync(CancellationToken cancellationToken)
        {
            Guardados++;
            return Task.CompletedTask;
        }
    }
}
