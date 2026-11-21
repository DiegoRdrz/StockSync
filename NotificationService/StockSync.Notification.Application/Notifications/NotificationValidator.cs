using StockSync.Notification.Domain.Entities;

namespace StockSync.Notification.Application.Notifications;

public static class NotificationValidator
{
    public static Dictionary<string, string[]> Validate(NotificationFilter filter)
    {
        var errors = new Dictionary<string, List<string>>();

        if (filter.Channel.HasValue && !Enum.IsDefined(filter.Channel.Value))
            AddError(errors, nameof(NotificationFilter.Channel), "The notification channel is invalid.");

        if (filter.EventType.HasValue && !Enum.IsDefined(filter.EventType.Value))
            AddError(errors, nameof(NotificationFilter.EventType), "The notification event type is invalid.");

        if (filter.Status.HasValue && !Enum.IsDefined(filter.Status.Value))
            AddError(errors, nameof(NotificationFilter.Status), "The notification status is invalid.");

        if (filter.BranchId == Guid.Empty)
            AddError(errors, nameof(NotificationFilter.BranchId), "BranchId cannot be an empty GUID.");

        if (filter.From.HasValue && filter.To.HasValue && filter.From.Value > filter.To.Value)
            AddError(errors, nameof(NotificationFilter.From), "The start date cannot be later than the end date.");

        if (filter.Page < 1)
            AddError(errors, nameof(NotificationFilter.Page), "Page must be at least 1.");

        if (filter.PageSize is < 1 or > NotificationFilter.MaxPageSize)
            AddError(errors, nameof(NotificationFilter.PageSize),
                $"PageSize must be between 1 and {NotificationFilter.MaxPageSize}.");

        return errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());
    }

    private static void AddError(Dictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var list))
        {
            list = [];
            errors[field] = list;
        }
        list.Add(message);
    }
}
