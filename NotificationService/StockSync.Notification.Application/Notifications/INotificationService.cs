using StockSync.Notification.Application.Common;

namespace StockSync.Notification.Application.Notifications;

public interface INotificationService
{
    Task<NotificationResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<NotificationResponse>> ListAsync(
        NotificationFilter filter,
        CancellationToken cancellationToken);
}
