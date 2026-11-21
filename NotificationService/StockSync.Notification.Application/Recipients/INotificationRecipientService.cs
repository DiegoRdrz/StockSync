using StockSync.Notification.Application.Common;

namespace StockSync.Notification.Application.Recipients;

public interface INotificationRecipientService
{
    Task<NotificationRecipientResponse> CreateAsync(
        NotificationRecipientRequest request,
        CancellationToken cancellationToken);

    Task<NotificationRecipientResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<NotificationRecipientResponse>> ListAsync(
        NotificationRecipientFilter filter,
        CancellationToken cancellationToken);

    Task<NotificationRecipientResponse> UpdateAsync(
        Guid id,
        NotificationRecipientRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
