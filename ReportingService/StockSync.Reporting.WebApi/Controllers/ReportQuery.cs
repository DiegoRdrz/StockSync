using StockSync.Reporting.Application;
using StockSync.Reporting.Application.Common.Exceptions;

namespace StockSync.Reporting.WebApi.Controllers;

public sealed class ReportQuery
{
    public string? From { get; init; }
    public string? To { get; init; }
    public Guid? BranchId { get; init; }

    public SalesFilter ToFilter(IQueryCollection query, TimeProvider clock)
    {
        ValidateValues(query, "from", "to", "branchId");
        return SalesFilter.Create(From, To, BranchId, clock.GetUtcNow().UtcDateTime);
    }

    internal static void ValidateValues(IQueryCollection query, params string[] fields)
    {
        foreach (var field in fields)
        {
            if (query.TryGetValue(field, out var values) && (values.Count != 1 || string.IsNullOrWhiteSpace(values[0])))
                throw new ValidationException(new Dictionary<string, string[]> { [field] = ["Supply one value."] });
        }
    }
}
