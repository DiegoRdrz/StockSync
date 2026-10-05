using StockSync.Inventory.Application.Common;
using StockSync.Inventory.Application.Common.Exceptions;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Repositories;

namespace StockSync.Inventory.Application.Productos;

public class ProductoService : IProductoService
{
    private readonly IProductoRepository _productoRepository;
    private readonly ICategoriaRepository _categoriaRepository;

    public ProductoService(IProductoRepository productoRepository, ICategoriaRepository categoriaRepository)
    {
        _productoRepository = productoRepository;
        _categoriaRepository = categoriaRepository;
    }

    public async Task<ProductoResponse> CrearAsync(ProductoRequest request, CancellationToken cancellationToken)
    {
        LanzarSiHayErrores(ProductoValidator.Validar(request));
        await AsegurarCategoriaValidaAsync(request.CategoriaId, cancellationToken);
        await AsegurarSkuDisponibleAsync(request.Sku, null, cancellationToken);

        var producto = Producto.Crear(
            request.Nombre,
            request.Sku,
            request.Descripcion,
            request.PrecioCompra,
            request.PrecioVenta,
            request.StockMinimo,
            request.CategoriaId);

        await _productoRepository.AgregarAsync(producto, cancellationToken);
        await _productoRepository.GuardarCambiosAsync(cancellationToken);

        return ProductoResponse.Desde(producto);
    }

    public async Task<ProductoResponse> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var producto = await _productoRepository.ObtenerPorIdAsync(id, cancellationToken)
            ?? throw ProductoNoEncontrado(id);

        return ProductoResponse.Desde(producto);
    }

    public async Task<ResultadoPaginado<ProductoResponse>> ListarAsync(ProductoFiltro filtro, CancellationToken cancellationToken)
    {
        LanzarSiHayErrores(ProductoValidator.Validar(filtro));

        var (items, total) = await _productoRepository.ListarAsync(
            filtro.Nombre,
            filtro.Sku,
            filtro.CategoriaId,
            (filtro.Pagina - 1) * filtro.TamanoPagina,
            filtro.TamanoPagina,
            cancellationToken);

        return new ResultadoPaginado<ProductoResponse>(
            items.Select(ProductoResponse.Desde).ToList(),
            filtro.Pagina,
            filtro.TamanoPagina,
            total);
    }

    public async Task<ProductoResponse> ActualizarAsync(Guid id, ProductoRequest request, CancellationToken cancellationToken)
    {
        LanzarSiHayErrores(ProductoValidator.Validar(request));

        var producto = await _productoRepository.ObtenerParaActualizarAsync(id, cancellationToken)
            ?? throw ProductoNoEncontrado(id);

        await AsegurarCategoriaValidaAsync(request.CategoriaId, cancellationToken);
        await AsegurarSkuDisponibleAsync(request.Sku, id, cancellationToken);

        producto.Actualizar(
            request.Nombre,
            request.Sku,
            request.Descripcion,
            request.PrecioCompra,
            request.PrecioVenta,
            request.StockMinimo,
            request.CategoriaId);

        await _productoRepository.GuardarCambiosAsync(cancellationToken);

        return ProductoResponse.Desde(producto);
    }

    public async Task EliminarAsync(Guid id, CancellationToken cancellationToken)
    {
        var producto = await _productoRepository.ObtenerParaActualizarAsync(id, cancellationToken)
            ?? throw ProductoNoEncontrado(id);

        producto.Desactivar();
        await _productoRepository.GuardarCambiosAsync(cancellationToken);
    }

    private async Task AsegurarCategoriaValidaAsync(Guid? categoriaId, CancellationToken cancellationToken)
    {
        if (categoriaId is null)
            return;

        if (!await _categoriaRepository.ExisteActivaAsync(categoriaId.Value, cancellationToken))
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(ProductoRequest.CategoriaId)] = [$"La categoría '{categoriaId}' no existe o está dada de baja."]
            });
    }

    private async Task AsegurarSkuDisponibleAsync(string sku, Guid? excluirId, CancellationToken cancellationToken)
    {
        var skuNormalizado = Producto.NormalizarSku(sku);
        if (await _productoRepository.ExisteSkuAsync(skuNormalizado, excluirId, cancellationToken))
            throw new ConflictException($"Ya existe un producto con el SKU '{skuNormalizado}'.");
    }

    private static void LanzarSiHayErrores(Dictionary<string, string[]> errores)
    {
        if (errores.Count > 0)
            throw new ValidationException(errores);
    }

    private static NotFoundException ProductoNoEncontrado(Guid id) =>
        new($"No se encontró el producto con id '{id}'.");
}
