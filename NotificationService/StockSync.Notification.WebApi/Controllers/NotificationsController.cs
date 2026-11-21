using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockSync.Notification.Application.Common;
using StockSync.Notification.Application.Notifications;

namespace StockSync.Notification.WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/notifications")]
[Produces("application/json")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<NotificationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<NotificationResponse>>> List(
        [FromQuery] NotificationFilter filter,
        CancellationToken cancellationToken) =>
        Ok(await _notificationService.ListAsync(filter, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(NotificationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotificationResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await _notificationService.GetByIdAsync(id, cancellationToken));
}
