namespace StockSync.Inventory.Application.Stocks;

public interface IStockService
{
    Task<StockResponse> CrearAsync(StockRequest request, CancellationToken cancellationToken);

    Task<StockResponse> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<StockResponse>> ListarPorSucursalAsync(Guid sucursalId, CancellationToken cancellationToken);

    Task<IReadOnlyList<StockResponse>> ListarPorProductoAsync(Guid productoId, CancellationToken cancellationToken);

    Task<StockResponse> ActualizarCantidadAsync(Guid id, StockCantidadRequest request, CancellationToken cancellationToken);

    Task EliminarAsync(Guid id, CancellationToken cancellationToken);
}
