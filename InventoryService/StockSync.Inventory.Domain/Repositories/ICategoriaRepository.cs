using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Domain.Repositories;

public interface ICategoriaRepository
{
    Task<Categoria?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Categoria?> ObtenerParaActualizarAsync(Guid id, CancellationToken cancellationToken);

    Task<(IReadOnlyList<Categoria> Items, int Total)> ListarAsync(
        string? nombre,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<bool> ExisteNombreAsync(string nombre, Guid? excluirId, CancellationToken cancellationToken);

    Task<bool> ExisteActivaAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> TieneProductosActivosAsync(Guid id, CancellationToken cancellationToken);

    Task AgregarAsync(Categoria categoria, CancellationToken cancellationToken);

    Task GuardarCambiosAsync(CancellationToken cancellationToken);
}
