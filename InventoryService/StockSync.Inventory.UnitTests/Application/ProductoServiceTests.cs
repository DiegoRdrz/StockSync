using StockSync.Inventory.Application.Common.Exceptions;
using StockSync.Inventory.Application.Productos;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Repositories;

namespace StockSync.Inventory.UnitTests.Application;

public class ProductoServiceTests
{
    private readonly FakeProductoRepository _repository = new();
    private readonly FakeCategoriaRepository _categoriaRepository = new();
    private readonly ProductoService _service;

    public ProductoServiceTests()
    {
        _service = new ProductoService(_repository, _categoriaRepository);
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
    public async Task CrearAsync_SkuDeProductoDadoDeBaja_PermiteReutilizarlo()
    {
        var original = await _service.CrearAsync(Request("FER-001"), CancellationToken.None);
        await _service.EliminarAsync(original.Id, CancellationToken.None);

        var nuevo = await _service.CrearAsync(Request(" fer-001 "), CancellationToken.None);

        Assert.NotEqual(original.Id, nuevo.Id);
        Assert.Equal("FER-001", nuevo.Sku);
    }

    [Fact]
    public async Task ActualizarAsync_SkuDeProductoDadoDeBaja_EsValido()
    {
        var baja = await _service.CrearAsync(Request("FER-001"), CancellationToken.None);
        await _service.EliminarAsync(baja.Id, CancellationToken.None);
        var otro = await _service.CrearAsync(Request("FER-002"), CancellationToken.None);

        var actualizado = await _service.ActualizarAsync(otro.Id, Request("FER-001"), CancellationToken.None);

        Assert.Equal("FER-001", actualizado.Sku);
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
            Task.FromResult(Productos.Any(p => p.Activo && p.Sku == sku && p.Id != excluirId));

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

    private sealed class FakeCategoriaRepository : ICategoriaRepository
    {
        public List<Categoria> Categorias { get; } = [];

        public Task<Categoria?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Categorias.FirstOrDefault(c => c.Id == id && c.Activo));

        public Task<Categoria?> ObtenerParaActualizarAsync(Guid id, CancellationToken cancellationToken) =>
            ObtenerPorIdAsync(id, cancellationToken);

        public Task<(IReadOnlyList<Categoria> Items, int Total)> ListarAsync(
            string? nombre,
            int skip,
            int take,
            CancellationToken cancellationToken)
        {
            var activas = Categorias.Where(c => c.Activo).ToList();
            IReadOnlyList<Categoria> items = activas.Skip(skip).Take(take).ToList();
            return Task.FromResult((items, activas.Count));
        }

        public Task<bool> ExisteNombreAsync(string nombre, Guid? excluirId, CancellationToken cancellationToken) =>
            Task.FromResult(Categorias.Any(c => Categoria.NormalizarNombre(c.Nombre) == nombre && c.Id != excluirId));

        public Task<bool> ExisteActivaAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Categorias.Any(c => c.Id == id && c.Activo));

        public Task<bool> TieneProductosActivosAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task AgregarAsync(Categoria categoria, CancellationToken cancellationToken)
        {
            Categorias.Add(categoria);
            return Task.CompletedTask;
        }

        public Task GuardarCambiosAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
