using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockSync.Orders.Application.Common;
using StockSync.Orders.Application.Lines;
using StockSync.Orders.Application.Orders;

namespace StockSync.Orders.WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/orders")]
[Produces("application/json")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderResponse>> Create(OrderRequest request, CancellationToken cancellationToken)
    {
        var order = await _orderService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await _orderService.GetByIdAsync(id, cancellationToken));

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OrderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<OrderResponse>>> List(
        [FromQuery] OrderFilter filter,
        CancellationToken cancellationToken) =>
        Ok(await _orderService.ListAsync(filter, cancellationToken));

    // The unit price is taken from the product's sale price in Inventory.
    [HttpPost("{id:guid}/lines")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<OrderResponse>> AddLine(
        Guid id, LineRequest request, CancellationToken cancellationToken) =>
        Ok(await _orderService.AddLineAsync(id, request, cancellationToken));

    [HttpPut("{id:guid}/lines/{lineId:guid}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderResponse>> UpdateLine(
        Guid id, Guid lineId, LineQuantityRequest request, CancellationToken cancellationToken) =>
        Ok(await _orderService.UpdateLineAsync(id, lineId, request, cancellationToken));

    [HttpDelete("{id:guid}/lines/{lineId:guid}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderResponse>> RemoveLine(
        Guid id, Guid lineId, CancellationToken cancellationToken) =>
        Ok(await _orderService.RemoveLineAsync(id, lineId, cancellationToken));

    // 200: confirmed and stock issued. 409: insufficient stock or invalid state; stock unchanged.
    // 503: Inventory unavailable; stock unchanged, or the order requires review when the result is unknown.
    [HttpPost("{id:guid}/confirm")]
    [Authorize(Roles = "Owner")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<OrderResponse>> Confirm(Guid id, CancellationToken cancellationToken) =>
        Ok(await _orderService.ConfirmAsync(id, cancellationToken));

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = "Owner")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderResponse>> Cancel(Guid id, CancellationToken cancellationToken) =>
        Ok(await _orderService.CancelAsync(id, cancellationToken));
}
