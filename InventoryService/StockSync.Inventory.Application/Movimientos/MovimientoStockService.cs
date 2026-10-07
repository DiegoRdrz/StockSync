using StockSync.Inventory.Application.Common;
using StockSync.Inventory.Application.Common.Exceptions;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Repositories;

namespace StockSync.Inventory.Application.Movimientos;

public class MovimientoStockService : IMovimientoStockService
{
    private readonly IStockRepository _stockRepository;
    private readonly IProductoRepository _productoRepository;
    private readonly IMovimientoStockRepository _movimientoRepository;

    public MovimientoStockService(IStockRepository stockRepository, IProductoRepository productoRepository,
        IMovimientoStockRepository movimientoRepository)
    {
        _stockRepository = stockRepository;
        _productoRepository = productoRepository;
        _movimientoRepository = movimientoRepository;
    }

    public Task<MovimientoStockResponse> RegistrarEntradaAsync(Guid stockId, MovimientoStockRequest request, CancellationToken cancellationToken) =>
        RegistrarAsync(stockId, TipoMovimientoStock.Entrada, request, cancellationToken);

    public Task<MovimientoStockResponse> RegistrarSalidaAsync(Guid stockId, MovimientoStockRequest request, CancellationToken cancellationToken) =>
        RegistrarAsync(stockId, TipoMovimientoStock.Salida, request, cancellationToken);

    private async Task<MovimientoStockResponse> RegistrarAsync(Guid stockId, TipoMovimientoStock tipo,
        MovimientoStockRequest request, CancellationToken cancellationToken)
    {
        LanzarSiHayErrores(MovimientoStockValidator.Validar(request));

        var stock = await _stockRepository.ObtenerParaActualizarAsync(stockId, cancellationToken)
            ?? throw StockNoEncontrado(stockId);

        if (await _productoRepository.ObtenerPorIdAsync(stock.ProductoId, cancellationToken) is null)
            throw new ConflictException($"El producto '{stock.ProductoId}' está dado de baja; su stock no admite movimientos.");

        var movimiento = stock.RegistrarMovimiento(tipo, request.Cantidad);
        await _movimientoRepository.AgregarAsync(movimiento, cancellationToken);

        // Ambos repositorios comparten el DbContext del request. Un único SaveChanges
        // confirma el saldo y su historial; si hay conflicto, se revierte todo.
        await _stockRepository.GuardarCambiosAsync(cancellationToken);
        return MovimientoStockResponse.Desde(movimiento);
    }

    public async Task<MovimientoStockResponse> ObtenerPorIdAsync(Guid stockId, Guid id, CancellationToken cancellationToken)
    {
        var movimiento = await _movimientoRepository.ObtenerPorIdAsync(stockId, id, cancellationToken)
            ?? throw new NotFoundException($"No se encontró el movimiento con id '{id}' para el stock '{stockId}'.");
        return MovimientoStockResponse.Desde(movimiento);
    }

    public async Task<ResultadoPaginado<MovimientoStockResponse>> ListarAsync(
        Guid stockId, MovimientoStockFiltro filtro, CancellationToken cancellationToken)
    {
        LanzarSiHayErrores(MovimientoStockValidator.Validar(filtro));
        _ = await _stockRepository.ObtenerPorIdAsync(stockId, cancellationToken)
            ?? throw StockNoEncontrado(stockId);

        var (items, total) = await _movimientoRepository.ListarAsync(
            stockId, (filtro.Pagina - 1) * filtro.TamanoPagina, filtro.TamanoPagina, cancellationToken);
        return new ResultadoPaginado<MovimientoStockResponse>(
            items.Select(MovimientoStockResponse.Desde).ToList(), filtro.Pagina, filtro.TamanoPagina, total);
    }

    public async Task<ResultadoPaginado<MovimientoStockListadoResponse>> ListarGeneralAsync(
        int pagina, CancellationToken cancellationToken)
    {
        const int tamanoPagina = 10;
        LanzarSiHayErrores(MovimientoStockValidator.Validar(new MovimientoStockFiltro(pagina, tamanoPagina)));
        var (items, total) = await _movimientoRepository.ListarGeneralAsync(
            (pagina - 1) * tamanoPagina, tamanoPagina, cancellationToken);
        return new ResultadoPaginado<MovimientoStockListadoResponse>(
            items.Select(MovimientoStockListadoResponse.Desde).ToList(), pagina, tamanoPagina, total);
    }

    private static void LanzarSiHayErrores(Dictionary<string, string[]> errores)
    {
        if (errores.Count > 0)
            throw new ValidationException(errores);
    }

    private static NotFoundException StockNoEncontrado(Guid id) => new($"No se encontró el stock con id '{id}'.");
}
