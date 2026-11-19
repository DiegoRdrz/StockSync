using System.Globalization;
using StockSync.Reporting.Application.Common;
using StockSync.Reporting.Application.Common.Exceptions;
using StockSync.Reporting.Domain;

namespace StockSync.Reporting.Application;

public sealed record SalesFilter(DateTime From, DateTime To, Guid? BranchId)
{
    public static SalesFilter Create(string? from, string? to, Guid? branchId, DateTime now)
    {
        var end = ParseUtc(to, "To") ?? now;
        if (from is null && end < DateTime.MinValue.AddDays(30))
            throw Invalid("From", "Specify a start date for this end date.");
        var start = ParseUtc(from, "From") ?? end.AddDays(-30);
        if (start > end || end - start > TimeSpan.FromDays(366))
            throw Invalid("From", "The inclusive date range must be ordered and at most 366 days.");
        if (branchId == Guid.Empty)
            throw Invalid("BranchId", "A valid branch GUID is required.");
        return new(start, end, branchId);
    }

    private static DateTime? ParseUtc(string? value, string field)
    {
        if (value is null) return null;
        string[] formats = ["yyyy-MM-dd", "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'",
            "yyyy-MM-dd'T'HH:mm:sszzz", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz"];
        if (!DateTimeOffset.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
            || date.Offset != TimeSpan.Zero)
            throw Invalid(field, "Use an ISO date or UTC timestamp.");
        return date.UtcDateTime;
    }

    public static void ValidatePage(int page, int pageSize)
    {
        if (page < 1) throw Invalid("Page", "Page must be positive.");
        if (pageSize is < 1 or > 100) throw Invalid("PageSize", "Page size must be between 1 and 100.");
    }

    public static void ValidateLimit(int limit)
    {
        if (limit is < 1 or > 50) throw Invalid("Limit", "Limit must be between 1 and 50.");
    }

    private static ValidationException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}

public sealed record SalesSummary(decimal TotalAmount, int SalesCount, decimal AverageTicket, long Units);
public sealed record DailySales(DateTime Day, decimal Amount, int Count, long Units);
public sealed record ProductSales(Guid ProductId, string ProductSku, string ProductName, long Quantity, decimal Revenue);
public sealed record SaleDetail(Guid OrderId, Guid BranchId, DateTime ConfirmedAt, decimal Total, List<SaleLine> Lines);

public interface ISalesReports
{
    Task<SalesSummary> SummaryAsync(SalesFilter filter, CancellationToken token);
    Task<List<DailySales>> DailyAsync(SalesFilter filter, CancellationToken token);
    Task<List<ProductSales>> TopAsync(SalesFilter filter, int limit, CancellationToken token);
    Task<PagedResult<SaleDetail>> DetailAsync(SalesFilter filter, int page, int pageSize, CancellationToken token);
}
