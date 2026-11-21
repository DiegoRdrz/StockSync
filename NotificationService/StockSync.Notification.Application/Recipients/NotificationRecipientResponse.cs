using StockSync.Notification.Domain.Entities;

namespace StockSync.Notification.Application.Recipients;

public sealed record NotificationRecipientResponse(
    Guid Id,
    string Name,
    string Channel,
    string Destination,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public static NotificationRecipientResponse From(NotificationRecipient recipient) => new(
        recipient.Id,
        recipient.Name,
        recipient.Channel.ToString(),
        recipient.Destination,
        recipient.IsActive,
        recipient.CreatedAt,
        recipient.UpdatedAt);
}
