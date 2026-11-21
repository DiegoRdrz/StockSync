namespace StockSync.Notification.Application.Notifications;

public sealed record NotificationResponse(
    Guid Id,
    Guid SourceEventId,
    string EventType,
    string Channel,
    string Recipient,
    string Subject,
    string Body,
    Guid BranchId,
    Guid ResourceId,
    string Status,
    DateTime CreatedAt,
    DateTime? SentAt,
    string? FailureReason)
{
    public static NotificationResponse From(Domain.Entities.Notification notification) => new(
        notification.Id,
        notification.SourceEventId,
        notification.EventType.ToString(),
        notification.Channel.ToString(),
        notification.Recipient,
        notification.Subject,
        notification.Body,
        notification.BranchId,
        notification.ResourceId,
        notification.Status.ToString(),
        notification.CreatedAt,
        notification.SentAt,
        notification.FailureReason);
}
