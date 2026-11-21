using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockSync.Inventory.Application.Common;
using StockSync.Inventory.Application.Stocks;

namespace StockSync.Inventory.WebApi.Controllers;

[Authorize]
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
    [Authorize(Roles = "Owner")]
    [ProducesResponseType(typeof(StockResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StockResponse>> Create(StockRequest request, CancellationToken cancellationToken)
    {
        var stock = await _stockService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = stock.Id }, stock);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(StockResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await _stockService.GetByIdAsync(id, cancellationToken));

    [HttpGet("branch/{branchId:guid}")]
    [ProducesResponseType(typeof(PagedResult<StockResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<StockResponse>>> ListByBranch(
        Guid branchId,
        [FromQuery] StockFilter filter,
        CancellationToken cancellationToken) =>
        Ok(await _stockService.ListByBranchAsync(branchId, filter, cancellationToken));

    [HttpGet("product/{productId:guid}")]
    [ProducesResponseType(typeof(PagedResult<StockResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<StockResponse>>> ListByProduct(
        Guid productId,
        [FromQuery] StockFilter filter,
        CancellationToken cancellationToken) =>
        Ok(await _stockService.ListByProductAsync(productId, filter, cancellationToken));

    [HttpGet("low-stock")]
    [ProducesResponseType(typeof(PagedResult<LowStockResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<LowStockResponse>>> ListLowStock(
        [FromQuery] LowStockFilter filter,
        CancellationToken cancellationToken) =>
        Ok(await _stockService.ListLowStockAsync(filter, cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Owner")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(StockResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockResponse>> UpdateQuantity(
        Guid id,
        StockQuantityRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _stockService.UpdateQuantityAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Owner")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _stockService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
