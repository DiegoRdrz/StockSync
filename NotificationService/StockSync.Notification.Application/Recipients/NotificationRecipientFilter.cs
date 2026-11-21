using StockSync.Notification.Domain.Entities;

namespace StockSync.Notification.Application.Recipients;

public class NotificationRecipientFilter
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public NotificationChannel? Channel { get; init; }
    public int Page { get; init; } = DefaultPage;
    public int PageSize { get; init; } = DefaultPageSize;
}
