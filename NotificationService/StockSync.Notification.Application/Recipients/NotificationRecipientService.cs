using StockSync.Notification.Application.Common;
using StockSync.Notification.Application.Common.Exceptions;
using StockSync.Notification.Domain.Entities;
using StockSync.Notification.Domain.Repositories;

namespace StockSync.Notification.Application.Recipients;

public class NotificationRecipientService : INotificationRecipientService
{
    private readonly INotificationRecipientRepository _recipientRepository;

    public NotificationRecipientService(INotificationRecipientRepository recipientRepository)
    {
        _recipientRepository = recipientRepository;
    }

    public async Task<NotificationRecipientResponse> CreateAsync(
        NotificationRecipientRequest request,
        CancellationToken cancellationToken)
    {
        ThrowIfErrors(NotificationRecipientValidator.Validate(request));
        await EnsureDestinationAvailableAsync(request.Channel, request.Destination, null, cancellationToken);

        var recipient = NotificationRecipient.Create(request.Name, request.Channel, request.Destination);

        await _recipientRepository.AddAsync(recipient, cancellationToken);
        await _recipientRepository.SaveChangesAsync(cancellationToken);

        return NotificationRecipientResponse.From(recipient);
    }

    public async Task<NotificationRecipientResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var recipient = await _recipientRepository.GetByIdAsync(id, cancellationToken)
            ?? throw RecipientNotFound(id);

        return NotificationRecipientResponse.From(recipient);
    }

    public async Task<PagedResult<NotificationRecipientResponse>> ListAsync(
        NotificationRecipientFilter filter,
        CancellationToken cancellationToken)
    {
        ThrowIfErrors(NotificationRecipientValidator.Validate(filter));

        var (items, total) = await _recipientRepository.ListAsync(
            filter.Channel,
            (filter.Page - 1) * filter.PageSize,
            filter.PageSize,
            cancellationToken);

        return new PagedResult<NotificationRecipientResponse>(
            items.Select(NotificationRecipientResponse.From).ToList(),
            filter.Page,
            filter.PageSize,
            total);
    }

    public async Task<NotificationRecipientResponse> UpdateAsync(
        Guid id,
        NotificationRecipientRequest request,
        CancellationToken cancellationToken)
    {
        ThrowIfErrors(NotificationRecipientValidator.Validate(request));

        var recipient = await _recipientRepository.GetForUpdateAsync(id, cancellationToken)
            ?? throw RecipientNotFound(id);

        await EnsureDestinationAvailableAsync(request.Channel, request.Destination, id, cancellationToken);

        recipient.Update(request.Name, request.Channel, request.Destination);
        await _recipientRepository.SaveChangesAsync(cancellationToken);

        return NotificationRecipientResponse.From(recipient);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var recipient = await _recipientRepository.GetForUpdateAsync(id, cancellationToken)
            ?? throw RecipientNotFound(id);

        recipient.Deactivate();
        await _recipientRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureDestinationAvailableAsync(
        NotificationChannel channel,
        string destination,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var normalized = NotificationRecipient.NormalizeDestination(channel, destination);
        if (await _recipientRepository.DestinationExistsAsync(channel, normalized, excludeId, cancellationToken))
            throw new ConflictException($"An active {channel} recipient for '{destination.Trim()}' already exists.");
    }

    private static void ThrowIfErrors(Dictionary<string, string[]> errors)
    {
        if (errors.Count > 0)
            throw new ValidationException(errors);
    }

    private static NotFoundException RecipientNotFound(Guid id) =>
        new($"Notification recipient '{id}' was not found.");
}
