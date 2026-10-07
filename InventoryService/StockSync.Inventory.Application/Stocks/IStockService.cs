using StockSync.Inventory.Application.Common;

namespace StockSync.Inventory.Application.Stocks;

public interface IStockService
{
    Task<StockResponse> CrearAsync(StockRequest request, CancellationToken cancellationToken);

    Task<StockResponse> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken);

    Task<ResultadoPaginado<StockResponse>> ListarPorSucursalAsync(
        Guid sucursalId, StockFiltro filtro, CancellationToken cancellationToken);

    Task<ResultadoPaginado<StockResponse>> ListarPorProductoAsync(
        Guid productoId, StockFiltro filtro, CancellationToken cancellationToken);

    Task<ResultadoPaginado<StockBajoMinimoResponse>> ListarBajoMinimoAsync(
        StockBajoMinimoFiltro filtro, CancellationToken cancellationToken);

    Task<StockResponse> ActualizarCantidadAsync(Guid id, StockCantidadRequest request, CancellationToken cancellationToken);

    Task EliminarAsync(Guid id, CancellationToken cancellationToken);
}
