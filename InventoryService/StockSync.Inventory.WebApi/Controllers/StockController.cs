using Microsoft.AspNetCore.Mvc;
using StockSync.Inventory.Application.Common;
using StockSync.Inventory.Application.Stocks;

namespace StockSync.Inventory.WebApi.Controllers;

[ApiController]
[Route("api/stock")]
[Produces("application/json")]
public class StockController : ControllerBase
{
    private readonly IStockService _stockService;

    public StockController(IStockService stockService)
    {
        _stockService = stockService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(StockResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StockResponse>> Crear(StockRequest request, CancellationToken cancellationToken)
    {
        var stock = await _stockService.CrearAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObtenerPorId), new { id = stock.Id }, stock);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(StockResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockResponse>> ObtenerPorId(Guid id, CancellationToken cancellationToken) =>
        Ok(await _stockService.ObtenerPorIdAsync(id, cancellationToken));

    [HttpGet("sucursal/{sucursalId:guid}")]
    [ProducesResponseType(typeof(ResultadoPaginado<StockResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ResultadoPaginado<StockResponse>>> ListarPorSucursal(
        Guid sucursalId,
        [FromQuery] StockFiltro filtro,
        CancellationToken cancellationToken) =>
        Ok(await _stockService.ListarPorSucursalAsync(sucursalId, filtro, cancellationToken));

    [HttpGet("producto/{productoId:guid}")]
    [ProducesResponseType(typeof(ResultadoPaginado<StockResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResultadoPaginado<StockResponse>>> ListarPorProducto(
        Guid productoId,
        [FromQuery] StockFiltro filtro,
        CancellationToken cancellationToken) =>
        Ok(await _stockService.ListarPorProductoAsync(productoId, filtro, cancellationToken));

    [HttpGet("bajo-minimo")]
    [ProducesResponseType(typeof(ResultadoPaginado<StockBajoMinimoResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ResultadoPaginado<StockBajoMinimoResponse>>> ListarBajoMinimo(
        [FromQuery] StockBajoMinimoFiltro filtro,
        CancellationToken cancellationToken) =>
        Ok(await _stockService.ListarBajoMinimoAsync(filtro, cancellationToken));

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(StockResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockResponse>> ActualizarCantidad(
        Guid id,
        StockCantidadRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _stockService.ActualizarCantidadAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken cancellationToken)
    {
        await _stockService.EliminarAsync(id, cancellationToken);
        return NoContent();
    }
}
