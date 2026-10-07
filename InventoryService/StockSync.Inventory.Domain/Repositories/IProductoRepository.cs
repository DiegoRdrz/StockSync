using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Domain.Repositories;

public interface IProductoRepository
{
    Task<Producto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Producto?> ObtenerParaActualizarAsync(Guid id, CancellationToken cancellationToken);

    Task<(IReadOnlyList<Producto> Items, int Total)> ListarAsync(
        string? nombre,
        string? sku,
        Guid? categoriaId,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<bool> ExisteSkuAsync(string sku, Guid? excluirId, CancellationToken cancellationToken);

    Task<bool> TieneExistenciasAsync(Guid id, CancellationToken cancellationToken);

    Task AgregarAsync(Producto producto, CancellationToken cancellationToken);

    Task GuardarCambiosAsync(CancellationToken cancellationToken);
}
