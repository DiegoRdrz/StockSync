using Microsoft.AspNetCore.Mvc;
using StockSync.Inventory.Application.Common;
using StockSync.Inventory.Application.Movimientos;

namespace StockSync.Inventory.WebApi.Controllers;

[ApiController]
[Route("api/stock/{stockId:guid}")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
public class MovimientosStockController : ControllerBase
{
    private readonly IMovimientoStockService _service;

    public MovimientosStockController(IMovimientoStockService service)
    {
        _service = service;
    }

    [HttpGet("~/api/movimientos")]
    [ProducesResponseType(typeof(ResultadoPaginado<MovimientoStockListadoResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ResultadoPaginado<MovimientoStockListadoResponse>>> ListarGeneral(
        CancellationToken cancellationToken) =>
        Ok(await _service.ListarGeneralAsync(1, cancellationToken));

    [HttpGet("~/api/movimientos/pagina/{pagina:int}")]
    [ProducesResponseType(typeof(ResultadoPaginado<MovimientoStockListadoResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ResultadoPaginado<MovimientoStockListadoResponse>>> ListarPagina(
        int pagina, CancellationToken cancellationToken) =>
        Ok(await _service.ListarGeneralAsync(pagina, cancellationToken));

    [HttpPost("entradas")]
    [ProducesResponseType(typeof(MovimientoStockResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MovimientoStockResponse>> RegistrarEntrada(
        Guid stockId, MovimientoStockRequest request, CancellationToken cancellationToken)
    {
        var movimiento = await _service.RegistrarEntradaAsync(stockId, request, cancellationToken);
        return CreatedAtAction(nameof(ObtenerPorId), new { stockId, id = movimiento.Id }, movimiento);
    }

    [HttpPost("salidas")]
    [ProducesResponseType(typeof(MovimientoStockResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MovimientoStockResponse>> RegistrarSalida(
        Guid stockId, MovimientoStockRequest request, CancellationToken cancellationToken)
    {
        var movimiento = await _service.RegistrarSalidaAsync(stockId, request, cancellationToken);
        return CreatedAtAction(nameof(ObtenerPorId), new { stockId, id = movimiento.Id }, movimiento);
    }

    [HttpGet("movimientos/{id:guid}")]
    [ProducesResponseType(typeof(MovimientoStockResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MovimientoStockResponse>> ObtenerPorId(
        Guid stockId, Guid id, CancellationToken cancellationToken) =>
        Ok(await _service.ObtenerPorIdAsync(stockId, id, cancellationToken));

    [HttpGet("movimientos")]
    [ProducesResponseType(typeof(ResultadoPaginado<MovimientoStockResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ResultadoPaginado<MovimientoStockResponse>>> Listar(
        Guid stockId, [FromQuery] MovimientoStockFiltro filtro, CancellationToken cancellationToken) =>
        Ok(await _service.ListarAsync(stockId, filtro, cancellationToken));
}
