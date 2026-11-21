using StockSync.Notification.Domain.Entities;

namespace StockSync.Notification.Application.Recipients;

public static class NotificationRecipientValidator
{
    public static Dictionary<string, string[]> Validate(NotificationRecipientRequest request)
    {
        var errors = new Dictionary<string, List<string>>();

        if (string.IsNullOrWhiteSpace(request.Name))
            AddError(errors, nameof(NotificationRecipientRequest.Name), "Recipient name is required.");
        else if (request.Name.Trim().Length > NotificationRecipient.NameMaxLength)
            AddError(errors, nameof(NotificationRecipientRequest.Name),
                $"Name cannot exceed {NotificationRecipient.NameMaxLength} characters.");

        if (!Enum.IsDefined(request.Channel))
            AddError(errors, nameof(NotificationRecipientRequest.Channel), "The notification channel is invalid.");

        if (string.IsNullOrWhiteSpace(request.Destination))
            AddError(errors, nameof(NotificationRecipientRequest.Destination), "Recipient destination is required.");
        else if (request.Destination.Trim().Length > NotificationRecipient.DestinationMaxLength)
            AddError(errors, nameof(NotificationRecipientRequest.Destination),
                $"Destination cannot exceed {NotificationRecipient.DestinationMaxLength} characters.");
        else if (Enum.IsDefined(request.Channel) &&
                 !NotificationRecipient.IsValidDestination(request.Channel, request.Destination))
            AddError(errors, nameof(NotificationRecipientRequest.Destination),
                request.Channel == NotificationChannel.Email
                    ? "The email address format is invalid."
                    : $"The WhatsApp number must contain between {NotificationRecipient.MinPhoneDigits} and {NotificationRecipient.MaxPhoneDigits} digits.");

        return errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());
    }

    public static Dictionary<string, string[]> Validate(NotificationRecipientFilter filter)
    {
        var errors = new Dictionary<string, List<string>>();

        if (filter.Channel.HasValue && !Enum.IsDefined(filter.Channel.Value))
            AddError(errors, nameof(NotificationRecipientFilter.Channel), "The notification channel is invalid.");

        if (filter.Page < 1)
            AddError(errors, nameof(NotificationRecipientFilter.Page), "Page must be at least 1.");

        if (filter.PageSize is < 1 or > NotificationRecipientFilter.MaxPageSize)
            AddError(errors, nameof(NotificationRecipientFilter.PageSize),
                $"PageSize must be between 1 and {NotificationRecipientFilter.MaxPageSize}.");

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
