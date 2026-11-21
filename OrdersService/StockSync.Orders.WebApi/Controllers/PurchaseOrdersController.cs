using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockSync.Orders.Application.Common;
using StockSync.Orders.Application.Lines;
using StockSync.Orders.Application.PurchaseOrders;

namespace StockSync.Orders.WebApi.Controllers;

[Authorize(Roles = "Owner")]
[ApiController]
[Route("api/purchase-orders")]
[Produces("application/json")]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IPurchaseOrderService _purchaseOrderService;

    public PurchaseOrdersController(IPurchaseOrderService purchaseOrderService)
    {
        _purchaseOrderService = purchaseOrderService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(PurchaseOrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PurchaseOrderResponse>> Create(
        PurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        var purchaseOrder = await _purchaseOrderService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = purchaseOrder.Id }, purchaseOrder);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PurchaseOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PurchaseOrderResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await _purchaseOrderService.GetByIdAsync(id, cancellationToken));

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<PurchaseOrderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<PurchaseOrderResponse>>> List(
        [FromQuery] PurchaseOrderFilter filter,
        CancellationToken cancellationToken) =>
        Ok(await _purchaseOrderService.ListAsync(filter, cancellationToken));

    // The unit cost is taken from the product's purchase price in Inventory.
    [HttpPost("{id:guid}/lines")]
    [ProducesResponseType(typeof(PurchaseOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<PurchaseOrderResponse>> AddLine(
        Guid id, LineRequest request, CancellationToken cancellationToken) =>
        Ok(await _purchaseOrderService.AddLineAsync(id, request, cancellationToken));

    [HttpPut("{id:guid}/lines/{lineId:guid}")]
    [ProducesResponseType(typeof(PurchaseOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PurchaseOrderResponse>> UpdateLine(
        Guid id, Guid lineId, LineQuantityRequest request, CancellationToken cancellationToken) =>
        Ok(await _purchaseOrderService.UpdateLineAsync(id, lineId, request, cancellationToken));

    [HttpDelete("{id:guid}/lines/{lineId:guid}")]
    [ProducesResponseType(typeof(PurchaseOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PurchaseOrderResponse>> RemoveLine(
        Guid id, Guid lineId, CancellationToken cancellationToken) =>
        Ok(await _purchaseOrderService.RemoveLineAsync(id, lineId, cancellationToken));

    // Sent to the supplier: lines can no longer change.
    [HttpPost("{id:guid}/place")]
    [ProducesResponseType(typeof(PurchaseOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PurchaseOrderResponse>> Place(Guid id, CancellationToken cancellationToken) =>
        Ok(await _purchaseOrderService.PlaceAsync(id, cancellationToken));

    // 200: goods received and stock recorded. 409: Inventory refused a line; stock unchanged.
    // 503: Inventory unavailable; stock unchanged, or the purchase order requires review when the result is unknown.
    [HttpPost("{id:guid}/receive")]
    [ProducesResponseType(typeof(PurchaseOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<PurchaseOrderResponse>> Receive(Guid id, CancellationToken cancellationToken) =>
        Ok(await _purchaseOrderService.ReceiveAsync(id, cancellationToken));

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(PurchaseOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PurchaseOrderResponse>> Cancel(Guid id, CancellationToken cancellationToken) =>
        Ok(await _purchaseOrderService.CancelAsync(id, cancellationToken));
}
