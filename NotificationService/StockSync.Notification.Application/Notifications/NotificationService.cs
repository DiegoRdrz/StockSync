using StockSync.Notification.Application.Common;
using StockSync.Notification.Application.Common.Exceptions;
using StockSync.Notification.Domain.Repositories;

namespace StockSync.Notification.Application.Notifications;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notificationRepository;

    public NotificationService(INotificationRepository notificationRepository)
    {
        _notificationRepository = notificationRepository;
    }

    public async Task<NotificationResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var notification = await _notificationRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Notification '{id}' was not found.");

        return NotificationResponse.From(notification);
    }

    public async Task<PagedResult<NotificationResponse>> ListAsync(
        NotificationFilter filter,
        CancellationToken cancellationToken)
    {
        var errors = NotificationValidator.Validate(filter);
        if (errors.Count > 0)
            throw new ValidationException(errors);

        var (items, total) = await _notificationRepository.ListAsync(
            filter.Channel,
            filter.EventType,
            filter.Status,
            filter.BranchId,
            ToUtc(filter.From),
            ToUtc(filter.To),
            (filter.Page - 1) * filter.PageSize,
            filter.PageSize,
            cancellationToken);

        return new PagedResult<NotificationResponse>(
            items.Select(NotificationResponse.From).ToList(),
            filter.Page,
            filter.PageSize,
            total);
    }

    private static DateTime? ToUtc(DateTime? value) =>
        value switch
        {
            null => null,
            { Kind: DateTimeKind.Utc } utc => utc,
            { Kind: DateTimeKind.Local } local => local.ToUniversalTime(),
            { } unspecified => DateTime.SpecifyKind(unspecified, DateTimeKind.Utc)
        };
}
