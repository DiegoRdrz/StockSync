using StockSync.Notification.Domain.Entities;

namespace StockSync.Notification.Application.Notifications;

public class NotificationFilter
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public NotificationChannel? Channel { get; init; }
    public NotificationEventType? EventType { get; init; }
    public NotificationStatus? Status { get; init; }
    public Guid? BranchId { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public int Page { get; init; } = DefaultPage;
    public int PageSize { get; init; } = DefaultPageSize;
}
