using System.Text.Json;

namespace StockSync.Reporting.Application;

public interface IReportingEvent<T> where T : IReportingEvent<T>
{
    static abstract string RoutingKey { get; }
    static abstract string ConsumerName { get; }
    Guid EventId { get; }
    Guid TenantId { get; }
    void Validate();
}

public static class ReportingEvent
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static T Parse<T>(ReadOnlyMemory<byte> body, string key, string? type, string? messageId, string? tenant)
        where T : class, IReportingEvent<T>
    {
        if (key != T.RoutingKey || type != T.RoutingKey)
            throw new InvalidDataException("Unexpected routing key or message type.");
        var message = JsonSerializer.Deserialize<T>(body.Span, Options);
        if (message is null || message.EventId == Guid.Empty || !Guid.TryParse(messageId, out var id) || id != message.EventId)
            throw new InvalidDataException("Missing or inconsistent event identity.");
        if (message.TenantId == Guid.Empty || !Guid.TryParse(tenant, out var owner) || owner != message.TenantId)
            throw new InvalidDataException("Missing or inconsistent tenant.");
        message.Validate();
        return message;
    }
}
