using StockSync.Notification.Domain.Common;
using StockSync.Notification.Domain.Exceptions;

namespace StockSync.Notification.Domain.Entities;

public class NotificationRecipient
{
    public const int NameMaxLength = 120;
    public const int DestinationMaxLength = 256;
    public const int MinPhoneDigits = 7;
    public const int MaxPhoneDigits = 15;

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public NotificationChannel Channel { get; private set; }
    public string Destination { get; private set; } = null!;
    // Persist the normalized destination so the unique index uses the application's comparison.
    public string NormalizedDestination { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private NotificationRecipient()
    {
    }

    public static NotificationRecipient Create(string name, NotificationChannel channel, string destination)
    {
        var recipient = new NotificationRecipient
        {
            Id = Guid.NewGuid(),
            IsActive = true,
            CreatedAt = UtcClock.Now()
        };
        recipient.SetDetails(name, channel, destination);
        return recipient;
    }

    public void Update(string name, NotificationChannel channel, string destination)
    {
        EnsureActive();
        SetDetails(name, channel, destination);
        UpdatedAt = UtcClock.Now();
    }

    public void Deactivate()
    {
        EnsureActive();
        IsActive = false;
        UpdatedAt = UtcClock.Now();
    }

    public static string NormalizeDestination(NotificationChannel channel, string destination)
    {
        var trimmed = destination.Trim();
        return channel switch
        {
            NotificationChannel.Email => trimmed.ToUpperInvariant(),
            NotificationChannel.WhatsApp => NormalizePhone(trimmed),
            _ => throw new DomainException("The notification channel is invalid.")
        };
    }

    public static bool IsValidDestination(NotificationChannel channel, string destination)
    {
        if (string.IsNullOrWhiteSpace(destination) || destination.Trim().Length > DestinationMaxLength)
            return false;

        var trimmed = destination.Trim();
        return channel switch
        {
            NotificationChannel.Email => IsValidEmail(trimmed),
            NotificationChannel.WhatsApp => IsValidPhone(trimmed),
            _ => false
        };
    }

    private void SetDetails(string name, NotificationChannel channel, string destination)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Recipient name is required.");
        if (name.Trim().Length > NameMaxLength)
            throw new DomainException($"Recipient name cannot exceed {NameMaxLength} characters.");
        if (!Enum.IsDefined(channel))
            throw new DomainException("The notification channel is invalid.");
        if (string.IsNullOrWhiteSpace(destination))
            throw new DomainException("Recipient destination is required.");
        if (destination.Trim().Length > DestinationMaxLength)
            throw new DomainException($"Recipient destination cannot exceed {DestinationMaxLength} characters.");
        if (!IsValidDestination(channel, destination))
            throw new DomainException(channel == NotificationChannel.Email
                ? "The email address format is invalid."
                : $"The WhatsApp number must contain between {MinPhoneDigits} and {MaxPhoneDigits} digits.");

        Name = name.Trim();
        Channel = channel;
        Destination = channel == NotificationChannel.WhatsApp
            ? NormalizePhone(destination.Trim())
            : destination.Trim();
        NormalizedDestination = NormalizeDestination(channel, destination);
    }

    private void EnsureActive()
    {
        if (!IsActive)
            throw new DomainException("The notification recipient is inactive.");
    }

    private static bool IsValidEmail(string value)
    {
        if (value.Any(char.IsWhiteSpace))
            return false;

        var atIndex = value.IndexOf('@');
        if (atIndex <= 0 || atIndex != value.LastIndexOf('@') || atIndex == value.Length - 1)
            return false;

        var domain = value[(atIndex + 1)..];
        return domain.Contains('.') && !domain.StartsWith('.') && !domain.EndsWith('.');
    }

    private static bool IsValidPhone(string value)
    {
        var normalized = NormalizePhone(value);
        var digits = normalized.StartsWith('+') ? normalized[1..] : normalized;
        return digits.Length >= MinPhoneDigits
            && digits.Length <= MaxPhoneDigits
            && digits.All(char.IsAsciiDigit);
    }

    private static string NormalizePhone(string value)
    {
        var chars = value.Where(c => !char.IsWhiteSpace(c) && c is not ('-' or '(' or ')')).ToArray();
        return new string(chars);
    }
}
