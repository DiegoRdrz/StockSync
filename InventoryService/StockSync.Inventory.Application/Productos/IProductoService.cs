using StockSync.Inventory.Application.Common;

namespace StockSync.Inventory.Application.Productos;

public interface IProductoService
{
    Task<ProductoResponse> CrearAsync(ProductoRequest request, CancellationToken cancellationToken);

    Task<ProductoResponse> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken);

    Task<ResultadoPaginado<ProductoResponse>> ListarAsync(ProductoFiltro filtro, CancellationToken cancellationToken);

    Task<ProductoResponse> ActualizarAsync(Guid id, ProductoRequest request, CancellationToken cancellationToken);

    Task EliminarAsync(Guid id, CancellationToken cancellationToken);
}
