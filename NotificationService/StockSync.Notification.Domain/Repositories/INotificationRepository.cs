using StockSync.Notification.Domain.Entities;

namespace StockSync.Notification.Domain.Repositories;

public interface INotificationRepository
{
    Task<Entities.Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<(IReadOnlyList<Entities.Notification> Items, int Total)> ListAsync(
        NotificationChannel? channel,
        NotificationEventType? eventType,
        NotificationStatus? status,
        Guid? branchId,
        DateTime? from,
        DateTime? to,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task AddAsync(Entities.Notification notification, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
