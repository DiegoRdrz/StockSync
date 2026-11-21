using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockSync.Notification.Application.Common;
using StockSync.Notification.Application.Recipients;

namespace StockSync.Notification.WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/notifications/recipients")]
[Produces("application/json")]
public class NotificationRecipientsController : ControllerBase
{
    private readonly INotificationRecipientService _recipientService;

    public NotificationRecipientsController(INotificationRecipientService recipientService)
    {
        _recipientService = recipientService;
    }

    [HttpPost]
    [Authorize(Roles = "Owner")]
    [ProducesResponseType(typeof(NotificationRecipientResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<NotificationRecipientResponse>> Create(
        NotificationRecipientRequest request,
        CancellationToken cancellationToken)
    {
        var recipient = await _recipientService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = recipient.Id }, recipient);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(NotificationRecipientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotificationRecipientResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await _recipientService.GetByIdAsync(id, cancellationToken));

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<NotificationRecipientResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<NotificationRecipientResponse>>> List(
        [FromQuery] NotificationRecipientFilter filter,
        CancellationToken cancellationToken) =>
        Ok(await _recipientService.ListAsync(filter, cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Owner")]
    [ProducesResponseType(typeof(NotificationRecipientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<NotificationRecipientResponse>> Update(
        Guid id,
        NotificationRecipientRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _recipientService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Owner")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _recipientService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
