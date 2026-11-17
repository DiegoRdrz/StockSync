using StockSync.Orders.Domain.Common;
using StockSync.Orders.Domain.Exceptions;

namespace StockSync.Orders.Domain.Entities;

// Supplier order. Receiving it records a stock receipt in Inventory for every line.
public class PurchaseOrder : IStockDocument
{
    public const int SupplierNameMaxLength = 150;
    public const int NotesMaxLength = 500;
    public const int ReviewReasonMaxLength = 1000;

    private readonly List<PurchaseOrderLine> _lines = [];

    public Guid Id { get; private set; }
    // Branch that receives the goods; owned by another service.
    public Guid BranchId { get; private set; }
    // There is no supplier service yet, so the supplier is free text.
    public string SupplierName { get; private set; } = null!;
    public PurchaseOrderStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public decimal Total { get; private set; }
    public string? ReviewReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? OrderedAt { get; private set; }
    public DateTime? ReceivedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }

    public IReadOnlyCollection<PurchaseOrderLine> Lines => _lines.AsReadOnly();

    IReadOnlyCollection<StockLine> IStockDocument.StockLines => _lines.AsReadOnly();

    private PurchaseOrder()
    {
    }

    public static PurchaseOrder Create(Guid branchId, string supplierName, string? notes)
    {
        if (branchId == Guid.Empty)
            throw new DomainException("The specified branch is invalid.");
        if (string.IsNullOrWhiteSpace(supplierName))
            throw new DomainException("Supplier name is required.");
        if (supplierName.Trim().Length > SupplierNameMaxLength)
            throw new DomainException($"Supplier name cannot exceed {SupplierNameMaxLength} characters.");

        return new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            SupplierName = supplierName.Trim(),
            Status = PurchaseOrderStatus.Draft,
            Notes = StockLines.NormalizeText(notes, NotesMaxLength, "Notes"),
            CreatedAt = UtcClock.Now()
        };
    }

    public PurchaseOrderLine AddLine(
        Guid productId,
        Guid stockId,
        string productName,
        string productSku,
        int quantity,
        decimal unitPrice)
    {
        EnsureStatus(PurchaseOrderStatus.Draft, "Lines can only be added to a draft purchase order.");
        StockLines.EnsureCanAdd(_lines, productId);

        var line = new PurchaseOrderLine(Id, StockLines.NextLineNumber(_lines), productId, stockId, productName, productSku, quantity, unitPrice);
        _lines.Add(line);
        Recalculate();
        return line;
    }

    public void UpdateLineQuantity(Guid lineId, int quantity)
    {
        EnsureStatus(PurchaseOrderStatus.Draft, "Lines can only be changed in a draft purchase order.");
        StockLines.Find(_lines, lineId).SetQuantity(quantity);
        Recalculate();
    }

    public void RemoveLine(Guid lineId)
    {
        EnsureStatus(PurchaseOrderStatus.Draft, "Lines can only be removed from a draft purchase order.");
        _lines.Remove(StockLines.Find(_lines, lineId));
        Recalculate();
    }

    public void Place()
    {
        EnsureStatus(PurchaseOrderStatus.Draft, "Only a draft purchase order can be placed.");
        if (_lines.Count == 0)
            throw new DomainException("A purchase order without lines cannot be placed.");

        Status = PurchaseOrderStatus.Ordered;
        OrderedAt = UtcClock.Now();
        Touch();
    }

    // Persist this state before calling Inventory: it blocks concurrent receptions.
    public void StartReceiving()
    {
        EnsureStatus(PurchaseOrderStatus.Ordered, "Only an ordered purchase order can be received.");

        Status = PurchaseOrderStatus.Receiving;
        Touch();
    }

    public void MarkLineApplied(Guid lineId, Guid movementId)
    {
        EnsureStatus(PurchaseOrderStatus.Receiving, "Stock can only be received while the purchase order is being received.");
        StockLines.Find(_lines, lineId).MarkApplied(movementId);
        Touch();
    }

    public void MarkLineCompensated(Guid lineId, Guid movementId)
    {
        EnsureStatus(PurchaseOrderStatus.Receiving, "Stock can only be compensated while the purchase order is being received.");
        StockLines.Find(_lines, lineId).MarkCompensated(movementId);
        Touch();
    }

    public void CompleteReceiving()
    {
        EnsureStatus(PurchaseOrderStatus.Receiving, "Only a purchase order being received can be completed.");
        if (_lines.Any(l => l.StockStatus != StockLineStatus.Applied))
            throw new DomainConflictException("Every line must have its stock received before the purchase order is completed.");

        Status = PurchaseOrderStatus.Received;
        ReceivedAt = UtcClock.Now();
        Touch();
    }

    // Inventory rejected a line and every received line was compensated: reception can be retried.
    public void RevertToOrdered()
    {
        EnsureStatus(PurchaseOrderStatus.Receiving, "Only a purchase order being received can return to ordered.");
        if (_lines.Any(l => l.StockStatus == StockLineStatus.Applied))
            throw new DomainConflictException("Received stock must be compensated before the purchase order returns to ordered.");

        foreach (var line in _lines)
            line.ResetStockStatus();
        Status = PurchaseOrderStatus.Ordered;
        Touch();
    }

    public void RequireReview(string reason)
    {
        EnsureStatus(PurchaseOrderStatus.Receiving, "Only a purchase order being received can require review.");

        Status = PurchaseOrderStatus.RequiresReview;
        ReviewReason = StockLines.ReviewReason(reason, ReviewReasonMaxLength);
        Touch();
    }

    // Nothing has reached Inventory before reception, so draft and ordered purchases can be cancelled.
    public void Cancel()
    {
        if (Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.Ordered))
            throw new DomainConflictException(
                $"Only a draft or ordered purchase order can be cancelled. Current status: {Status}.");

        Status = PurchaseOrderStatus.Cancelled;
        CancelledAt = UtcClock.Now();
        Touch();
    }

    private void EnsureStatus(PurchaseOrderStatus expected, string message)
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
