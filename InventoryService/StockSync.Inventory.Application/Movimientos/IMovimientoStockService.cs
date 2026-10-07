using StockSync.Inventory.Application.Common;

namespace StockSync.Inventory.Application.Movimientos;

public interface IMovimientoStockService
{
    Task<MovimientoStockResponse> RegistrarEntradaAsync(Guid stockId, MovimientoStockRequest request, CancellationToken cancellationToken);
    Task<MovimientoStockResponse> RegistrarSalidaAsync(Guid stockId, MovimientoStockRequest request, CancellationToken cancellationToken);
    Task<MovimientoStockResponse> ObtenerPorIdAsync(Guid stockId, Guid id, CancellationToken cancellationToken);
    Task<ResultadoPaginado<MovimientoStockResponse>> ListarAsync(Guid stockId, MovimientoStockFiltro filtro, CancellationToken cancellationToken);
    Task<ResultadoPaginado<MovimientoStockListadoResponse>> ListarGeneralAsync(int pagina, CancellationToken cancellationToken);
}
