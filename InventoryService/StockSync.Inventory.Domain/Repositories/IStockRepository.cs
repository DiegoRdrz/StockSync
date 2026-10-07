using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Domain.Repositories;

public interface IStockRepository
{
    Task<Stock?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken);

    // Bloquea la fila hasta que termine la transacción en curso (ver IUnidadDeTrabajo): las operaciones
    // concurrentes sobre el mismo stock esperan su turno y leen el saldo ya confirmado.
    Task<Stock?> ObtenerParaActualizarAsync(Guid id, CancellationToken cancellationToken);

    Task<Stock?> ObtenerPorProductoYSucursalAsync(Guid productoId, Guid sucursalId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Stock>> ListarPorSucursalAsync(Guid sucursalId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Stock>> ListarPorProductoAsync(Guid productoId, CancellationToken cancellationToken);

    Task<bool> ExisteAsync(Guid productoId, Guid sucursalId, CancellationToken cancellationToken);

    Task AgregarAsync(Stock stock, CancellationToken cancellationToken);

    void Eliminar(Stock stock);

    Task GuardarCambiosAsync(CancellationToken cancellationToken);
}
