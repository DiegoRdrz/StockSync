using StockSync.Orders.Application.Common.Exceptions;

namespace StockSync.Orders.Application.Common;

// Collects field errors in the format returned by ValidationProblemDetails.
internal sealed class ValidationErrors
{
    private readonly Dictionary<string, List<string>> _errors = new();

    public void Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var messages))
            _errors[field] = messages = [];
        messages.Add(message);
    }

    public void AddRange(Dictionary<string, string[]> errors)
    {
        foreach (var (field, messages) in errors)
        foreach (var message in messages)
            Add(field, message);
    }

    public Dictionary<string, string[]> ToDictionary() =>
        _errors.ToDictionary(e => e.Key, e => e.Value.ToArray());

    public static void ThrowIfAny(Dictionary<string, string[]> errors)
    {
        if (errors.Count > 0)
            throw new ValidationException(errors);
    }

    public static ValidationException Single(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
