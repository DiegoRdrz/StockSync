namespace StockSync.Orders.Application.Common;

// Shared list filters for sales and purchase orders. From and To apply to the creation date.
public abstract class DocumentFilter
{
    public const int MaxPageSize = 100;

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public Guid? BranchId { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
}

public static class DocumentFilterValidator
{
    public static Dictionary<string, string[]> Validate(DocumentFilter filter)
    {
        var errors = new ValidationErrors();

        if (filter.Page < 1)
            errors.Add(nameof(filter.Page), "Page must be at least 1.");

        if (filter.PageSize < 1 || filter.PageSize > DocumentFilter.MaxPageSize)
            errors.Add(nameof(filter.PageSize), $"Page size must be between 1 and {DocumentFilter.MaxPageSize}.");

        // The offset (Page - 1) * PageSize is an int and must not overflow.
        if (filter.Page > 0 && filter.PageSize > 0 &&
            (long)(filter.Page - 1) * filter.PageSize > int.MaxValue)
            errors.Add(nameof(filter.Page), "The requested page exceeds the allowed limit.");

        if (filter.From.HasValue && filter.To.HasValue && ToUtc(filter.From) > ToUtc(filter.To))
            errors.Add(nameof(filter.From), "From must be earlier than or equal to To.");

        return errors.ToDictionary();
    }

    public static int Offset(DocumentFilter filter) => (filter.Page - 1) * filter.PageSize;

    // Query dates without an offset are interpreted as UTC; PostgreSQL timestamptz requires UTC values.
    public static DateTime? ToUtc(DateTime? value) => value?.Kind switch
    {
        null => null,
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.Value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value!.Value, DateTimeKind.Utc)
    };
}
