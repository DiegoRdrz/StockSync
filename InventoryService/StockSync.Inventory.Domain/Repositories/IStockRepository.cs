using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Domain.Repositories;

public interface IStockRepository
{
    Task<Stock?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Stock?> ObtenerParaActualizarAsync(Guid id, CancellationToken cancellationToken);

    Task<Stock?> ObtenerPorProductoYSucursalAsync(Guid productoId, Guid sucursalId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Stock>> ListarPorSucursalAsync(Guid sucursalId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Stock>> ListarPorProductoAsync(Guid productoId, CancellationToken cancellationToken);

    Task<bool> ExisteAsync(Guid productoId, Guid sucursalId, CancellationToken cancellationToken);

    Task AgregarAsync(Stock stock, CancellationToken cancellationToken);

    void Eliminar(Stock stock);

    Task GuardarCambiosAsync(CancellationToken cancellationToken);
}
