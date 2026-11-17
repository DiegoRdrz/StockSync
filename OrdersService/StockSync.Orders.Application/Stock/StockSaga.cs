using StockSync.Orders.Application.Inventory;
using StockSync.Orders.Domain.Entities;

namespace StockSync.Orders.Application.Stock;

public enum StockDirection
{
    // Sales remove units from the branch.
    Issue = 1,
    // Purchases add units to the branch.
    Receipt = 2
}

public enum StockSagaOutcome
{
    // Every line has its movement in Inventory.
    Completed = 1,
    // A line was refused and every applied movement was reversed: stock is unchanged.
    Reverted = 2,
    // At least one movement has an unknown result: a person must reconcile it.
    Uncertain = 3
}

public sealed record StockSagaResult(StockSagaOutcome Outcome, string? Error = null, bool InventoryUnavailable = false);

// Orchestrates one Inventory movement per line (saga with compensation).
//
// Inventory has no batch or idempotent movement endpoint, so:
// - progress is saved after every call, which records which movements exist;
// - when Inventory refuses a line, the movements already applied are reversed with the
//   opposite movement (an issue is compensated with a receipt, and vice versa);
// - an unknown result is never retried automatically, because a retry could move stock twice.
//   The document is left for manual review instead.
public sealed class StockSaga
{
    private readonly IInventoryClient _inventory;

    public StockSaga(IInventoryClient inventory)
    {
        _inventory = inventory;
    }

    // The caller must not cancel a running saga: aborting a request halfway would leave
    // movements with an unknown result. Inventory calls therefore use CancellationToken.None.
    public async Task<StockSagaResult> ExecuteAsync(
        IStockDocument document,
        StockDirection direction,
        Func<Task> saveProgress)
    {
        // Process lines in the order they were added; the database does not guarantee collection order.
        var pendingLines = document.StockLines
            .Where(l => l.StockStatus == StockLineStatus.Pending)
            .OrderBy(l => l.LineNumber)
            .ToList();

        foreach (var line in pendingLines)
        {
            var result = await MoveAsync(direction, line);
            switch (result.Outcome)
            {
                case StockMovementOutcome.Applied:
                    document.MarkLineApplied(line.Id, result.MovementId!.Value);
                    await saveProgress();
                    break;

                case StockMovementOutcome.Unknown:
                    return new StockSagaResult(StockSagaOutcome.Uncertain,
                        $"Unknown result for product '{line.ProductSku}': {result.Error}");

                default:
                    var notSent = result.Outcome == StockMovementOutcome.NotApplied;
                    var reason = notSent
                        ? $"Product '{line.ProductSku}' could not be sent to Inventory: {result.Error}"
                        : $"Inventory refused product '{line.ProductSku}': {result.Error}";
                    return await CompensateAsync(document, direction, saveProgress, reason, inventoryUnavailable: notSent);
            }
        }

        return new StockSagaResult(StockSagaOutcome.Completed);
    }

    // Reverse applied movements, newest first.
    private async Task<StockSagaResult> CompensateAsync(
        IStockDocument document,
        StockDirection direction,
        Func<Task> saveProgress,
        string reason,
        bool inventoryUnavailable)
    {
        var appliedLines = document.StockLines
            .Where(l => l.StockStatus == StockLineStatus.Applied)
            .OrderByDescending(l => l.LineNumber)
            .ToList();

        foreach (var line in appliedLines)
        {
            var result = await MoveAsync(Opposite(direction), line);
            if (result.Outcome != StockMovementOutcome.Applied)
                return new StockSagaResult(StockSagaOutcome.Uncertain,
                    $"{reason} Compensation for product '{line.ProductSku}' failed: {result.Error}");

            document.MarkLineCompensated(line.Id, result.MovementId!.Value);
            await saveProgress();
        }

        return new StockSagaResult(StockSagaOutcome.Reverted, reason, inventoryUnavailable);
    }

    private Task<StockMovementResult> MoveAsync(StockDirection direction, StockLine line) =>
        direction == StockDirection.Issue
            ? _inventory.IssueAsync(line.StockId, line.Quantity, CancellationToken.None)
            : _inventory.ReceiveAsync(line.StockId, line.Quantity, CancellationToken.None);

    private static StockDirection Opposite(StockDirection direction) =>
        direction == StockDirection.Issue ? StockDirection.Receipt : StockDirection.Issue;
}
