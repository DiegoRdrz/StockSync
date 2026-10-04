using Microsoft.AspNetCore.Mvc;
using StockSync.Inventory.Application.Common;
using StockSync.Inventory.Application.Productos;

namespace StockSync.Inventory.WebApi.Controllers;

[ApiController]
[Route("api/productos")]
[Produces("application/json")]
public class ProductosController : ControllerBase
{
    private readonly IProductoService _productoService;

    public ProductosController(IProductoService productoService)
    {
        _productoService = productoService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ProductoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductoResponse>> Crear(ProductoRequest request, CancellationToken cancellationToken)
    {
        var producto = await _productoService.CrearAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObtenerPorId), new { id = producto.Id }, producto);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductoResponse>> ObtenerPorId(Guid id, CancellationToken cancellationToken) =>
        Ok(await _productoService.ObtenerPorIdAsync(id, cancellationToken));

    [HttpGet]
    [ProducesResponseType(typeof(ResultadoPaginado<ProductoResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ResultadoPaginado<ProductoResponse>>> Listar(
        [FromQuery] ProductoFiltro filtro,
        CancellationToken cancellationToken) =>
        Ok(await _productoService.ListarAsync(filtro, cancellationToken));

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ProductoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductoResponse>> Actualizar(
        Guid id,
        ProductoRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _productoService.ActualizarAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken cancellationToken)
    {
        await _productoService.EliminarAsync(id, cancellationToken);
        return NoContent();
    }
}
