using StockSync.Inventory.Application.Common.Exceptions;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Repositories;

namespace StockSync.Inventory.Application.Stocks;

public class StockService : IStockService
{
    private readonly IStockRepository _stockRepository;
    private readonly IProductoRepository _productoRepository;
    private readonly IMovimientoStockRepository _movimientoRepository;

    public StockService(
        IStockRepository stockRepository,
        IProductoRepository productoRepository,
        IMovimientoStockRepository movimientoRepository)
    {
        _stockRepository = stockRepository;
        _productoRepository = productoRepository;
        _movimientoRepository = movimientoRepository;
    }

    public async Task<StockResponse> CrearAsync(StockRequest request, CancellationToken cancellationToken)
    {
        LanzarSiHayErrores(StockValidator.Validar(request));

        // El repositorio de productos solo devuelve productos activos: uno dado de baja se trata como inexistente.
        _ = await _productoRepository.ObtenerPorIdAsync(request.ProductoId, cancellationToken)
            ?? throw new NotFoundException($"No se encontró el producto con id '{request.ProductoId}'.");

        if (await _stockRepository.ExisteAsync(request.ProductoId, request.SucursalId, cancellationToken))
            throw new ConflictException(
                $"El producto '{request.ProductoId}' ya está asignado a la sucursal '{request.SucursalId}'.");

        // El saldo inicial se registra como ajuste desde 0 para que quede en el historial.
        var stock = Stock.Crear(request.ProductoId, request.SucursalId, 0);
        var saldoInicial = stock.ActualizarCantidad(request.Cantidad);

        await _stockRepository.AgregarAsync(stock, cancellationToken);
        if (saldoInicial is not null)
            await _movimientoRepository.AgregarAsync(saldoInicial, cancellationToken);
        await _stockRepository.GuardarCambiosAsync(cancellationToken);

        return StockResponse.Desde(stock);
    }

    public async Task<StockResponse> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var stock = await _stockRepository.ObtenerPorIdAsync(id, cancellationToken)
            ?? throw StockNoEncontrado(id);

        return StockResponse.Desde(stock);
    }

    public async Task<IReadOnlyList<StockResponse>> ListarPorSucursalAsync(Guid sucursalId, CancellationToken cancellationToken)
    {
        var items = await _stockRepository.ListarPorSucursalAsync(sucursalId, cancellationToken);

        return items.Select(StockResponse.Desde).ToList();
    }

    public async Task<IReadOnlyList<StockResponse>> ListarPorProductoAsync(Guid productoId, CancellationToken cancellationToken)
    {
        var items = await _stockRepository.ListarPorProductoAsync(productoId, cancellationToken);

        return items.Select(StockResponse.Desde).ToList();
    }

    public async Task<StockResponse> ActualizarCantidadAsync(Guid id, StockCantidadRequest request, CancellationToken cancellationToken)
    {
        LanzarSiHayErrores(StockValidator.Validar(request));

        var stock = await _stockRepository.ObtenerParaActualizarAsync(id, cancellationToken)
            ?? throw StockNoEncontrado(id);

        var ajuste = stock.ActualizarCantidad(request.NuevaCantidad);
        if (ajuste is not null)
            await _movimientoRepository.AgregarAsync(ajuste, cancellationToken);
        await _stockRepository.GuardarCambiosAsync(cancellationToken);

        return StockResponse.Desde(stock);
    }

    // Solo elimina la asignación a la sucursal; el producto no se toca.
    public async Task EliminarAsync(Guid id, CancellationToken cancellationToken)
    {
        var stock = await _stockRepository.ObtenerParaActualizarAsync(id, cancellationToken)
            ?? throw StockNoEncontrado(id);

        _stockRepository.Eliminar(stock);
        await _stockRepository.GuardarCambiosAsync(cancellationToken);
    }

    private static void LanzarSiHayErrores(Dictionary<string, string[]> errores)
    {
        if (errores.Count > 0)
            throw new ValidationException(errores);
    }

    private static NotFoundException StockNoEncontrado(Guid id) =>
        new($"No se encontró el stock con id '{id}'.");
}
