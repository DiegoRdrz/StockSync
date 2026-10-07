using Microsoft.AspNetCore.Mvc;
using StockSync.Inventory.Application.Categorias;
using StockSync.Inventory.Application.Common;
using StockSync.Inventory.Application.Productos;

namespace StockSync.Inventory.WebApi.Controllers;

[ApiController]
[Route("api/categorias")]
[Produces("application/json")]
public class CategoriasController : ControllerBase
{
    private readonly ICategoriaService _categoriaService;
    private readonly IProductoService _productoService;

    public CategoriasController(ICategoriaService categoriaService, IProductoService productoService)
    {
        _categoriaService = categoriaService;
        _productoService = productoService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(CategoriaResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoriaResponse>> Crear(CategoriaRequest request, CancellationToken cancellationToken)
    {
        var categoria = await _categoriaService.CrearAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObtenerPorId), new { id = categoria.Id }, categoria);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CategoriaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoriaResponse>> ObtenerPorId(Guid id, CancellationToken cancellationToken) =>
        Ok(await _categoriaService.ObtenerPorIdAsync(id, cancellationToken));

    [HttpGet]
    [ProducesResponseType(typeof(ResultadoPaginado<CategoriaResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ResultadoPaginado<CategoriaResponse>>> Listar(
        [FromQuery] CategoriaFiltro filtro,
        CancellationToken cancellationToken) =>
        Ok(await _categoriaService.ListarAsync(filtro, cancellationToken));

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CategoriaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoriaResponse>> Actualizar(
        Guid id,
        CategoriaRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _categoriaService.ActualizarAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken cancellationToken)
    {
        await _categoriaService.EliminarAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/productos")]
    [ProducesResponseType(typeof(ResultadoPaginado<ProductoResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResultadoPaginado<ProductoResponse>>> ListarProductosPorCategoria(
        Guid id,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await _productoService.ListarPorCategoriaAsync(id, pagina, tamanoPagina, cancellationToken));
}
