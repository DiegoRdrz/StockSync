using StockSync.Orders.Domain.Common;
using StockSync.Orders.Domain.Exceptions;

namespace StockSync.Orders.Domain.Entities;

// Sales order. A sale is confirmed only after Inventory has recorded an issue for every line,
// so a confirmed order never exceeds the available stock.
public class Order : IStockDocument
{
    public const int NotesMaxLength = 500;
    public const int ReviewReasonMaxLength = 1000;

    private readonly List<OrderLine> _lines = [];

    public Guid Id { get; private set; }
    // Branch owned by another service; Orders neither models nor validates it.
    public Guid BranchId { get; private set; }
    public OrderStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public decimal Total { get; private set; }
    public string? ReviewReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }

    public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();

    IReadOnlyCollection<StockLine> IStockDocument.StockLines => _lines.AsReadOnly();

    private Order()
    {
    }

    public static Order Create(Guid branchId, string? notes)
    {
        if (branchId == Guid.Empty)
            throw new DomainException("The specified branch is invalid.");

        return new Order
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            Status = OrderStatus.Draft,
            Notes = StockLines.NormalizeText(notes, NotesMaxLength, "Notes"),
            CreatedAt = UtcClock.Now()
        };
    }

    public OrderLine AddLine(
        Guid productId,
        Guid stockId,
        string productName,
        string productSku,
        int quantity,
        decimal unitPrice)
    {
        EnsureStatus(OrderStatus.Draft, "Lines can only be added to a draft order.");
        StockLines.EnsureCanAdd(_lines, productId);

        var line = new OrderLine(Id, StockLines.NextLineNumber(_lines), productId, stockId, productName, productSku, quantity, unitPrice);
        _lines.Add(line);
        Recalculate();
        return line;
    }

    public void UpdateLineQuantity(Guid lineId, int quantity)
    {
        EnsureStatus(OrderStatus.Draft, "Lines can only be changed in a draft order.");
        StockLines.Find(_lines, lineId).SetQuantity(quantity);
        Recalculate();
    }

    public void RemoveLine(Guid lineId)
    {
        EnsureStatus(OrderStatus.Draft, "Lines can only be removed from a draft order.");
        _lines.Remove(StockLines.Find(_lines, lineId));
        Recalculate();
    }

    // Persist this state before calling Inventory: it blocks edits and concurrent confirmations.
    public void StartConfirmation()
    {
        EnsureStatus(OrderStatus.Draft, "Only a draft order can be confirmed.");
        if (_lines.Count == 0)
            throw new DomainException("An order without lines cannot be confirmed.");

        Status = OrderStatus.Confirming;
        Touch();
    }

    public void MarkLineApplied(Guid lineId, Guid movementId)
    {
        EnsureStatus(OrderStatus.Confirming, "Stock can only be issued while the order is being confirmed.");
        StockLines.Find(_lines, lineId).MarkApplied(movementId);
        Touch();
    }

    public void MarkLineCompensated(Guid lineId, Guid movementId)
    {
        EnsureStatus(OrderStatus.Confirming, "Stock can only be compensated while the order is being confirmed.");
        StockLines.Find(_lines, lineId).MarkCompensated(movementId);
        Touch();
    }

    public void CompleteConfirmation()
    {
        EnsureStatus(OrderStatus.Confirming, "Only an order being confirmed can be completed.");
        if (_lines.Any(l => l.StockStatus != StockLineStatus.Applied))
            throw new DomainConflictException("Every line must have its stock issued before the sale is confirmed.");

        Status = OrderStatus.Confirmed;
        ConfirmedAt = UtcClock.Now();
        Touch();
    }

    // Inventory rejected a line and every issued line was compensated: the order can be edited again.
    public void RevertToDraft()
    {
        EnsureStatus(OrderStatus.Confirming, "Only an order being confirmed can return to draft.");
        if (_lines.Any(l => l.StockStatus == StockLineStatus.Applied))
            throw new DomainConflictException("Issued stock must be compensated before the order returns to draft.");

        foreach (var line in _lines)
            line.ResetStockStatus();
        Status = OrderStatus.Draft;
        Touch();
    }

    public void RequireReview(string reason)
    {
        EnsureStatus(OrderStatus.Confirming, "Only an order being confirmed can require review.");

        Status = OrderStatus.RequiresReview;
        ReviewReason = StockLines.ReviewReason(reason, ReviewReasonMaxLength);
        Touch();
    }

    // A confirmed sale has already issued stock; reversing it is a return, not a cancellation.
    public void Cancel()
    {
        if (Status == OrderStatus.Confirmed)
            throw new DomainConflictException("A confirmed order cannot be cancelled.");
        EnsureStatus(OrderStatus.Draft, "Only a draft order can be cancelled.");

        Status = OrderStatus.Cancelled;
        CancelledAt = UtcClock.Now();
        Touch();
    }

    private void EnsureStatus(OrderStatus expected, string message)
    {
        if (Status != expected)
            throw new DomainConflictException($"{message} Current status: {Status}.");
    }

    private void Recalculate()
    {
        Total = StockLines.Total(_lines);
        Touch();
    }

    private void Touch() => UpdatedAt = UtcClock.Now();
}
