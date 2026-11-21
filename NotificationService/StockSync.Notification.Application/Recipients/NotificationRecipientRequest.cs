using StockSync.Notification.Domain.Entities;

namespace StockSync.Notification.Application.Recipients;

public sealed record NotificationRecipientRequest(
    string Name,
    NotificationChannel Channel,
    string Destination);
