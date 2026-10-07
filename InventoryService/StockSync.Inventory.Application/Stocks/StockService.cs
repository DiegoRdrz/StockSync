using StockSync.Inventory.Application.Common.Exceptions;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Repositories;

namespace StockSync.Inventory.Application.Stocks;

public class StockService : IStockService
{
    private readonly IStockRepository _stockRepository;
    private readonly IProductoRepository _productoRepository;
    private readonly IMovimientoStockRepository _movimientoRepository;
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;

    public StockService(
        IStockRepository stockRepository,
        IProductoRepository productoRepository,
        IMovimientoStockRepository movimientoRepository,
        IUnidadDeTrabajo unidadDeTrabajo)
    {
        _stockRepository = stockRepository;
        _productoRepository = productoRepository;
        _movimientoRepository = movimientoRepository;
        _unidadDeTrabajo = unidadDeTrabajo;
    }

    public async Task<StockResponse> CrearAsync(StockRequest request, CancellationToken cancellationToken)
    {
        LanzarSiHayErrores(StockValidator.Validar(request));

        // Una referencia inválida en el cuerpo es un error de validación (400), no un recurso de la URL inexistente (404).
        if (await _productoRepository.ObtenerPorIdAsync(request.ProductoId, cancellationToken) is null)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(StockRequest.ProductoId)] = [$"El producto '{request.ProductoId}' no existe o está dado de baja."]
            });

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

    public Task<StockResponse> ActualizarCantidadAsync(Guid id, StockCantidadRequest request, CancellationToken cancellationToken)
    {
        LanzarSiHayErrores(StockValidator.Validar(request));

        return _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            var stock = await _stockRepository.ObtenerParaActualizarAsync(id, cancellationToken)
                ?? throw StockNoEncontrado(id);
            await AsegurarProductoActivoAsync(stock.ProductoId, cancellationToken);

            var ajuste = stock.ActualizarCantidad(request.NuevaCantidad);
            if (ajuste is not null)
                await _movimientoRepository.AgregarAsync(ajuste, cancellationToken);
            await _stockRepository.GuardarCambiosAsync(cancellationToken);

            return StockResponse.Desde(stock);
        }, cancellationToken);
    }

    // Solo elimina la asignación a la sucursal; el producto no se toca.
    public Task EliminarAsync(Guid id, CancellationToken cancellationToken) =>
        _unidadDeTrabajo.EjecutarEnTransaccionAsync(async () =>
        {
            var stock = await _stockRepository.ObtenerParaActualizarAsync(id, cancellationToken)
                ?? throw StockNoEncontrado(id);

            // Borrar una asignación con unidades las haría desaparecer sin ningún movimiento que lo explique.
            if (stock.Cantidad > 0)
                throw new ConflictException("No se puede eliminar la asignación porque tiene existencias.");

            _stockRepository.Eliminar(stock);
            await _stockRepository.GuardarCambiosAsync(cancellationToken);
        }, cancellationToken);

    private async Task AsegurarProductoActivoAsync(Guid productoId, CancellationToken cancellationToken)
    {
        if (await _productoRepository.ObtenerPorIdAsync(productoId, cancellationToken) is null)
            throw new ConflictException($"El producto '{productoId}' está dado de baja; su stock no admite cambios.");
    }

    private static void LanzarSiHayErrores(Dictionary<string, string[]> errores)
    {
        if (errores.Count > 0)
            throw new ValidationException(errores);
    }

    private static NotFoundException StockNoEncontrado(Guid id) =>
        new($"No se encontró el stock con id '{id}'.");
}
