using StockSync.Inventory.Application.Common;

namespace StockSync.Inventory.Application.Categorias;

public interface ICategoriaService
{
    Task<CategoriaResponse> CrearAsync(CategoriaRequest request, CancellationToken cancellationToken);

    Task<CategoriaResponse> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken);

    Task<ResultadoPaginado<CategoriaResponse>> ListarAsync(CategoriaFiltro filtro, CancellationToken cancellationToken);

    Task<CategoriaResponse> ActualizarAsync(Guid id, CategoriaRequest request, CancellationToken cancellationToken);

    Task EliminarAsync(Guid id, CancellationToken cancellationToken);
}
