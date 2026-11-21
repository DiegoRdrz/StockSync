using StockSync.Notification.Domain.Common;
using StockSync.Notification.Domain.Exceptions;

namespace StockSync.Notification.Domain.Entities;

public class Notification
{
    public const int RecipientMaxLength = 256;
    public const int SubjectMaxLength = 200;
    public const int BodyMaxLength = 2000;
    public const int FailureReasonMaxLength = 500;

    public Guid Id { get; private set; }
    public Guid SourceEventId { get; private set; }
    public NotificationEventType EventType { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public string Recipient { get; private set; } = null!;
    public string Subject { get; private set; } = null!;
    public string Body { get; private set; } = null!;
    public Guid BranchId { get; private set; }
    public Guid ResourceId { get; private set; }
    public NotificationStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? SentAt { get; private set; }
    public string? FailureReason { get; private set; }

    private Notification()
    {
    }

    public static Notification Create(
        Guid sourceEventId,
        NotificationEventType eventType,
        NotificationChannel channel,
        string recipient,
        string subject,
        string body,
        Guid branchId,
        Guid resourceId)
    {
        if (sourceEventId == Guid.Empty)
            throw new DomainException("The source event ID is required.");
        if (!Enum.IsDefined(eventType))
            throw new DomainException("The notification event type is invalid.");
        if (!Enum.IsDefined(channel))
            throw new DomainException("The notification channel is invalid.");
        if (string.IsNullOrWhiteSpace(recipient))
            throw new DomainException("The notification recipient is required.");
        if (recipient.Trim().Length > RecipientMaxLength)
            throw new DomainException($"The notification recipient cannot exceed {RecipientMaxLength} characters.");
        if (string.IsNullOrWhiteSpace(subject))
            throw new DomainException("The notification subject is required.");
        if (subject.Trim().Length > SubjectMaxLength)
            throw new DomainException($"The notification subject cannot exceed {SubjectMaxLength} characters.");
        if (string.IsNullOrWhiteSpace(body))
            throw new DomainException("The notification body is required.");
        if (body.Trim().Length > BodyMaxLength)
            throw new DomainException($"The notification body cannot exceed {BodyMaxLength} characters.");
        if (branchId == Guid.Empty)
            throw new DomainException("The branch ID is required.");
        if (resourceId == Guid.Empty)
            throw new DomainException("The resource ID is required.");

        return new Notification
        {
            Id = Guid.NewGuid(),
            SourceEventId = sourceEventId,
            EventType = eventType,
            Channel = channel,
            Recipient = recipient.Trim(),
            Subject = subject.Trim(),
            Body = body.Trim(),
            BranchId = branchId,
            ResourceId = resourceId,
            Status = NotificationStatus.Pending,
            CreatedAt = UtcClock.Now()
        };
    }

    public void MarkSent()
    {
        if (Status == NotificationStatus.Sent)
            throw new DomainException("The notification has already been sent.");

        Status = NotificationStatus.Sent;
        SentAt = UtcClock.Now();
        FailureReason = null;
    }

    public void MarkFailed(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("A failure reason is required when marking a notification as failed.");

        var trimmed = reason.Trim();
        Status = NotificationStatus.Failed;
        SentAt = null;
        FailureReason = trimmed.Length <= FailureReasonMaxLength
            ? trimmed
            : trimmed[..FailureReasonMaxLength];
    }
}
