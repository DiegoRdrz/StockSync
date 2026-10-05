using StockSync.Inventory.Application.Categorias;
using StockSync.Inventory.Application.Common.Exceptions;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Repositories;

namespace StockSync.Inventory.UnitTests.Application;

public class CategoriaServiceTests
{
    private readonly FakeCategoriaRepository _repository = new();
    private readonly CategoriaService _service;

    public CategoriaServiceTests()
    {
        _service = new CategoriaService(_repository);
    }

    [Fact]
    public async Task CrearAsync_CategoriaValida_GuardaYDevuelveResponse()
    {
        var response = await _service.CrearAsync(new("Herramientas", null), CancellationToken.None);

        Assert.Equal("Herramientas", response.Nombre);
        Assert.Single(_repository.Categorias);
    }

    [Fact]
    public async Task CrearAsync_NombreDuplicado_LanzaConflictException()
    {
        await _service.CrearAsync(new("Herramientas", null), CancellationToken.None);

        // Sin importar mayúsculas o espacios
        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.CrearAsync(new(" herramientas ", null), CancellationToken.None));
    }

    [Fact]
    public async Task EliminarAsync_ConProductosActivos_LanzaConflictException()
    {
        var categoria = await _service.CrearAsync(new("Herramientas", null), CancellationToken.None);
        _repository.SimularProductosActivos(true);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.EliminarAsync(categoria.Id, CancellationToken.None));
    }

    [Fact]
    public async Task ObtenerPorIdAsync_Inexistente_LanzaNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ObtenerPorIdAsync(Guid.NewGuid(), CancellationToken.None));
    }

    private sealed class FakeCategoriaRepository : ICategoriaRepository
    {
        public List<Categoria> Categorias { get; } = [];
        private bool _tieneProductosActivos = false;

        public void SimularProductosActivos(bool tiene) => _tieneProductosActivos = tiene;

        public Task<Categoria?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Categorias.FirstOrDefault(c => c.Id == id && c.Activo));

        public Task<Categoria?> ObtenerParaActualizarAsync(Guid id, CancellationToken cancellationToken) =>
            ObtenerPorIdAsync(id, cancellationToken);

        public Task<(IReadOnlyList<Categoria> Items, int Total)> ListarAsync(string? nombre, int skip, int take, CancellationToken cancellationToken) =>
            Task.FromResult(((IReadOnlyList<Categoria>)Categorias.Where(c => c.Activo).ToList(), Categorias.Count(c => c.Activo)));

        public Task<bool> ExisteNombreAsync(string nombre, Guid? excluirId, CancellationToken cancellationToken) =>
            Task.FromResult(Categorias.Any(c => Categoria.NormalizarNombre(c.Nombre) == nombre && c.Id != excluirId));

        public Task<bool> ExisteActivaAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Categorias.Any(c => c.Id == id && c.Activo));

        public Task<bool> TieneProductosActivosAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(_tieneProductosActivos);

        public Task AgregarAsync(Categoria categoria, CancellationToken cancellationToken)
        {
            Categorias.Add(categoria);
            return Task.CompletedTask;
        }

        public Task GuardarCambiosAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
