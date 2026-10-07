using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Domain.Repositories;

public interface IMovimientoStockRepository
{
    // Agrega al mismo contexto que Stock; GuardarCambiosAsync de Stock confirma ambos de forma atómica.
    Task AgregarAsync(MovimientoStock movimiento, CancellationToken cancellationToken);

    Task<MovimientoStock?> ObtenerPorIdAsync(Guid stockId, Guid id, CancellationToken cancellationToken);

    Task<(IReadOnlyList<MovimientoStock> Items, int Total)> ListarAsync(
        Guid stockId, int skip, int take, CancellationToken cancellationToken);

    Task<(IReadOnlyList<MovimientoStockDetalle> Items, int Total)> ListarGeneralAsync(
        int skip, int take, CancellationToken cancellationToken);
}
