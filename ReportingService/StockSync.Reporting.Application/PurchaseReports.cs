namespace StockSync.Reporting.Application;

public sealed record PurchaseSummary(decimal TotalAmount, int ReceivedPurchases, long UnitsReceived);
public sealed record DailyPurchases(DateTime Day, decimal Amount, int Count, long UnitsReceived);

public interface IPurchaseReports
{
    Task<PurchaseSummary> SummaryAsync(SalesFilter filter, CancellationToken token);
    Task<List<DailyPurchases>> DailyAsync(SalesFilter filter, CancellationToken token);
}
