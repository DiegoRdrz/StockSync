using StockSync.Notification.Domain.Entities;

namespace StockSync.Notification.Domain.Repositories;

public interface INotificationRecipientRepository
{
    Task<NotificationRecipient?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<NotificationRecipient?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<(IReadOnlyList<NotificationRecipient> Items, int Total)> ListAsync(
        NotificationChannel? channel,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<NotificationRecipient>> ListActiveAsync(CancellationToken cancellationToken);

    Task<bool> DestinationExistsAsync(
        NotificationChannel channel,
        string normalizedDestination,
        Guid? excludeId,
        CancellationToken cancellationToken);

    Task AddAsync(NotificationRecipient recipient, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
