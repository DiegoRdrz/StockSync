using StockSync.Notification.Domain.Entities;

namespace StockSync.Notification.Application.Channels;

public interface INotificationChannelSender
{
    NotificationChannel Channel { get; }

    Task SendAsync(
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken);
}
