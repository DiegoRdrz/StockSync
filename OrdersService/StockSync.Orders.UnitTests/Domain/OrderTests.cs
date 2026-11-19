using StockSync.Orders.Domain.Entities;
using StockSync.Orders.Domain.Exceptions;

namespace StockSync.Orders.UnitTests.Domain;

public class OrderTests
{
    private static Order CreateDraft() => Order.Create(Guid.NewGuid(), null);

    private static OrderLine AddLine(Order order, int quantity = 2, decimal unitPrice = 10m) =>
        order.AddLine(Guid.NewGuid(), Guid.NewGuid(), "Hammer", "HAM-001", quantity, unitPrice);

    [Fact]
    public void Create_WithValidData_StartsAsEmptyDraft()
    {
        var branchId = Guid.NewGuid();

        var order = Order.Create(branchId, "  Counter sale  ");

        Assert.NotEqual(Guid.Empty, order.Id);
        Assert.Equal(branchId, order.BranchId);
        Assert.Equal(OrderStatus.Draft, order.Status);
        Assert.Equal("Counter sale", order.Notes);
        Assert.Equal(0m, order.Total);
        Assert.Empty(order.Lines);
    }

    [Fact]
    public void Create_WithEmptyBranch_Throws()
    {
        Assert.Throws<DomainException>(() => Order.Create(Guid.Empty, null));
    }

    [Fact]
    public void Create_WithTooLongNotes_Throws()
    {
        Assert.Throws<DomainException>(() => Order.Create(Guid.NewGuid(), new string('x', Order.NotesMaxLength + 1)));
    }

    [Fact]
    public void AddLine_CalculatesLineAndOrderTotals()
    {
        var order = CreateDraft();

        var line = AddLine(order, quantity: 3, unitPrice: 2.50m);
        AddLine(order, quantity: 1, unitPrice: 10m);

        Assert.Equal(7.50m, line.LineTotal);
        Assert.Equal(17.50m, order.Total);
        Assert.Equal(StockLineStatus.Pending, line.StockStatus);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    [InlineData(1, -5)]
    [InlineData(1, 1.234)]
    public void AddLine_WithInvalidQuantityOrPrice_Throws(int quantity, decimal unitPrice)
    {
        var order = CreateDraft();

        Assert.Throws<DomainException>(() => AddLine(order, quantity, unitPrice));
        Assert.Empty(order.Lines);
    }

    [Fact]
    public void AddLine_SameProductTwice_ThrowsConflict()
    {
        var order = CreateDraft();
        var productId = Guid.NewGuid();
        order.AddLine(productId, Guid.NewGuid(), "Hammer", "HAM-001", 1, 10m);

        Assert.Throws<DomainConflictException>(() =>
            order.AddLine(productId, Guid.NewGuid(), "Hammer", "HAM-001", 1, 10m));
    }

    [Fact]
    public void AddLine_NumbersLinesAfterTheHighestCurrentLine()
    {
        var order = CreateDraft();
        var first = AddLine(order);
        var second = AddLine(order);
        order.RemoveLine(first.Id);

        var third = AddLine(order);

        Assert.Equal(1, first.LineNumber);
        Assert.Equal(2, second.LineNumber);
        Assert.Equal(3, third.LineNumber);
    }

    [Fact]
    public void UpdateAndRemoveLine_RecalculateTotal()
    {
        var order = CreateDraft();
        var first = AddLine(order, quantity: 1, unitPrice: 10m);
        var second = AddLine(order, quantity: 1, unitPrice: 5m);

        order.UpdateLineQuantity(first.Id, 4);
        Assert.Equal(45m, order.Total);

        order.RemoveLine(second.Id);
        Assert.Equal(40m, order.Total);
        Assert.Single(order.Lines);
    }

    [Fact]
    public void StartConfirmation_WithoutLines_Throws()
    {
        var order = CreateDraft();

        Assert.Throws<DomainException>(order.StartConfirmation);
        Assert.Equal(OrderStatus.Draft, order.Status);
    }

    [Fact]
    public void Confirmation_WhenEveryLineIsIssued_ConfirmsOrder()
    {
        var order = CreateDraft();
        var line = AddLine(order);
        var movementId = Guid.NewGuid();

        order.StartConfirmation();
        order.MarkLineApplied(line.Id, movementId);
        order.CompleteConfirmation();

        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.NotNull(order.ConfirmedAt);
        Assert.Equal(movementId, line.StockMovementId);
    }

    [Fact]
    public void CompleteConfirmation_WithPendingLine_Throws()
    {
        var order = CreateDraft();
        AddLine(order);
        order.StartConfirmation();

        Assert.Throws<DomainConflictException>(order.CompleteConfirmation);
        Assert.Equal(OrderStatus.Confirming, order.Status);
    }

    [Fact]
    public void ConfirmingOrder_CannotBeEditedConfirmedAgainOrCancelled()
    {
        var order = CreateDraft();
        var line = AddLine(order);
        order.StartConfirmation();

        Assert.Throws<DomainConflictException>(() => AddLine(order));
        Assert.Throws<DomainConflictException>(() => order.UpdateLineQuantity(line.Id, 5));
        Assert.Throws<DomainConflictException>(() => order.RemoveLine(line.Id));
        Assert.Throws<DomainConflictException>(order.StartConfirmation);
        Assert.Throws<DomainConflictException>(order.Cancel);
    }

    [Fact]
    public void ConfirmedOrder_CannotBeConfirmedTwiceOrCancelled()
    {
        var order = CreateDraft();
        var line = AddLine(order);
        order.StartConfirmation();
        order.MarkLineApplied(line.Id, Guid.NewGuid());
        order.CompleteConfirmation();

        Assert.Throws<DomainConflictException>(order.StartConfirmation);
        var exception = Assert.Throws<DomainConflictException>(order.Cancel);
        Assert.Contains("confirmed order cannot be cancelled", exception.Message);
    }

    [Fact]
    public void RevertToDraft_WithIssuedLine_Throws()
    {
        var order = CreateDraft();
        var line = AddLine(order);
        order.StartConfirmation();
        order.MarkLineApplied(line.Id, Guid.NewGuid());

        Assert.Throws<DomainConflictException>(order.RevertToDraft);
    }

    [Fact]
    public void RevertToDraft_AfterCompensation_ResetsLines()
    {
        var order = CreateDraft();
        var line = AddLine(order);
        order.StartConfirmation();
        order.MarkLineApplied(line.Id, Guid.NewGuid());
        order.MarkLineCompensated(line.Id, Guid.NewGuid());

        order.RevertToDraft();

        Assert.Equal(OrderStatus.Draft, order.Status);
        Assert.Equal(StockLineStatus.Pending, line.StockStatus);
        Assert.Null(line.StockMovementId);
        Assert.Null(line.CompensationMovementId);
    }

    [Fact]
    public void RequireReview_KeepsLineProgressAndReason()
    {
        var order = CreateDraft();
        var line = AddLine(order);
        order.StartConfirmation();
        order.MarkLineApplied(line.Id, Guid.NewGuid());

        order.RequireReview(new string('x', Order.ReviewReasonMaxLength + 50));

        Assert.Equal(OrderStatus.RequiresReview, order.Status);
        Assert.Equal(Order.ReviewReasonMaxLength, order.ReviewReason!.Length);
        Assert.Equal(StockLineStatus.Applied, line.StockStatus);
    }

    [Fact]
    public void Cancel_Draft_SetsCancelled()
    {
        var order = CreateDraft();

        order.Cancel();

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.NotNull(order.CancelledAt);
        Assert.Throws<DomainConflictException>(() => AddLine(order));
    }
}
