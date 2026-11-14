using Microsoft.AspNetCore.Mvc;
using StockSync.Inventory.Application.Common;
using StockSync.Inventory.Application.Movements;

namespace StockSync.Inventory.WebApi.Controllers;

[ApiController]
[Route("api/stock/{stockId:guid}")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
public class StockMovementsController : ControllerBase
{
    private readonly IStockMovementService _service;

    public StockMovementsController(IStockMovementService service)
    {
        _service = service;
    }

    [HttpGet("~/api/movements")]
    [ProducesResponseType(typeof(PagedResult<StockMovementListResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<StockMovementListResponse>>> ListAll(
        CancellationToken cancellationToken) =>
        Ok(await _service.ListAllAsync(1, cancellationToken));

    [HttpGet("~/api/movements/page/{page:int}")]
    [ProducesResponseType(typeof(PagedResult<StockMovementListResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<StockMovementListResponse>>> ListPage(
        int page, CancellationToken cancellationToken) =>
        Ok(await _service.ListAllAsync(page, cancellationToken));

    [HttpPost("receipts")]
    [ProducesResponseType(typeof(StockMovementResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StockMovementResponse>> RecordReceipt(
        Guid stockId, StockMovementRequest request, CancellationToken cancellationToken)
    {
        var movement = await _service.RecordReceiptAsync(stockId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { stockId, id = movement.Id }, movement);
    }

    [HttpPost("issues")]
    [ProducesResponseType(typeof(StockMovementResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StockMovementResponse>> RecordIssue(
        Guid stockId, StockMovementRequest request, CancellationToken cancellationToken)
    {
        var movement = await _service.RecordIssueAsync(stockId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { stockId, id = movement.Id }, movement);
    }

    [HttpGet("movements/{id:guid}")]
    [ProducesResponseType(typeof(StockMovementResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<StockMovementResponse>> GetById(
        Guid stockId, Guid id, CancellationToken cancellationToken) =>
        Ok(await _service.GetByIdAsync(stockId, id, cancellationToken));

    [HttpGet("movements")]
    [ProducesResponseType(typeof(PagedResult<StockMovementResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<StockMovementResponse>>> List(
        Guid stockId, [FromQuery] StockMovementFilter filter, CancellationToken cancellationToken) =>
        Ok(await _service.ListAsync(stockId, filter, cancellationToken));
}
