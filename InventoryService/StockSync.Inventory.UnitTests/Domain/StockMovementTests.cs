using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Exceptions;

namespace StockSync.Inventory.UnitTests.Domain;

public class StockMovementTests
{
    [Theory]
    [InlineData(StockMovementType.Receipt, 5, 15)]
    [InlineData(StockMovementType.Issue, 4, 6)]
    [InlineData(StockMovementType.Issue, 10, 0)]
    public void RecordMovement_Valid_UpdatesBalanceAndRecordsHistory(StockMovementType type, int quantity, int expected)
    {
        var stock = Stock.Create(Guid.NewGuid(), Guid.NewGuid(), 10);
        var before = DateTime.UtcNow;

        var movement = stock.RecordMovement(type, quantity);

        Assert.Equal(expected, stock.Quantity);
        Assert.NotEqual(Guid.Empty, movement.Id);
        Assert.Equal(stock.Id, movement.StockId);
        Assert.Equal(type, movement.Type);
        Assert.Equal(quantity, movement.Quantity);
        Assert.Equal(10, movement.PreviousQuantity);
        Assert.Equal(expected, movement.NewQuantity);
        Assert.InRange(movement.Date, before.AddTicks(-TimeSpan.TicksPerMicrosecond), DateTime.UtcNow);
        Assert.Equal(DateTimeKind.Utc, movement.Date.Kind);
        Assert.Equal(0, movement.Date.Ticks % TimeSpan.TicksPerMicrosecond);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(10, 11)]
    public void Issue_InsufficientStock_RejectsWithoutModification(int available, int requested)
    {
        var stock = Stock.Create(Guid.NewGuid(), Guid.NewGuid(), available);

        Assert.Throws<InsufficientStockException>(() => stock.RecordMovement(StockMovementType.Issue, requested));
        Assert.Equal(available, stock.Quantity);
    }

    [Theory]
    [InlineData(StockMovementType.Receipt, 0)]
    [InlineData(StockMovementType.Receipt, -1)]
    [InlineData(StockMovementType.Issue, 0)]
    [InlineData(StockMovementType.Issue, -1)]
    [InlineData((StockMovementType)99, 1)]
    [InlineData(StockMovementType.Adjustment, 1)]
    public void RecordMovement_Invalid_DoesNotModify(StockMovementType type, int quantity)
    {
        var stock = Stock.Create(Guid.NewGuid(), Guid.NewGuid(), 10);

        Assert.Throws<DomainException>(() => stock.RecordMovement(type, quantity));
        Assert.Equal(10, stock.Quantity);
    }

    [Fact]
    public void Receipt_Overflow_DoesNotModify()
    {
        var stock = Stock.Create(Guid.NewGuid(), Guid.NewGuid(), int.MaxValue);

        Assert.Throws<DomainException>(() => stock.RecordMovement(StockMovementType.Receipt, 1));
        Assert.Equal(int.MaxValue, stock.Quantity);
    }

    [Fact]
    public void SuccessiveMovements_PreserveHistoricalBalances()
    {
        var stock = Stock.Create(Guid.NewGuid(), Guid.NewGuid(), 0);
        var entry = stock.RecordMovement(StockMovementType.Receipt, 10);
        var issue = stock.RecordMovement(StockMovementType.Issue, 10);

        Assert.Equal(10, entry.NewQuantity);
        Assert.Equal(10, issue.PreviousQuantity);
        Assert.Equal(0, stock.Quantity);
        Assert.NotEqual(entry.Id, issue.Id);
    }
}
