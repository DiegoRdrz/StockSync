using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Exceptions;

namespace StockSync.Inventory.UnitTests.Domain;

public class StockTests
{
    private static Stock CreateValid(Guid? productId = null, Guid? branchId = null, int quantity = 20) =>
        Stock.Create(productId ?? Guid.NewGuid(), branchId ?? Guid.NewGuid(), quantity);

    [Fact]
    public void Create_WithValidData_InitializesStock()
    {
        var productId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        var stock = CreateValid(productId, branchId, 20);

        Assert.NotEqual(Guid.Empty, stock.Id);
        Assert.Equal(productId, stock.ProductId);
        Assert.Equal(branchId, stock.BranchId);
        Assert.Equal(20, stock.Quantity);
    }

    [Fact]
    public void Create_WithZeroQuantity_IsValid()
    {
        var stock = CreateValid(quantity: 0);

        Assert.Equal(0, stock.Quantity);
    }

    [Fact]
    public void Create_WithNegativeQuantity_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => CreateValid(quantity: -1));
    }

    [Fact]
    public void Create_WithEmptyProduct_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => CreateValid(productId: Guid.Empty));
    }

    [Fact]
    public void Create_WithEmptyBranch_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => CreateValid(branchId: Guid.Empty));
    }

    [Fact]
    public void UpdateQuantity_WithValidQuantity_UpdatesOnlyQuantity()
    {
        var stock = CreateValid(quantity: 20);
        var id = stock.Id;
        var productId = stock.ProductId;
        var branchId = stock.BranchId;

        stock.UpdateQuantity(5);

        Assert.Equal(5, stock.Quantity);
        Assert.Equal(id, stock.Id);
        Assert.Equal(productId, stock.ProductId);
        Assert.Equal(branchId, stock.BranchId);
    }

    [Fact]
    public void UpdateQuantity_WithZeroQuantity_IsValid()
    {
        var stock = CreateValid(quantity: 20);

        stock.UpdateQuantity(0);

        Assert.Equal(0, stock.Quantity);
    }

    [Theory]
    [InlineData(20, 35, 15)]
    [InlineData(20, 5, 15)]
    [InlineData(0, 12, 12)]
    public void UpdateQuantity_RecordsAdjustmentWithPreviousAndNewBalances(int initial, int updated, int difference)
    {
        var stock = CreateValid(quantity: initial);

        var adjustment = stock.UpdateQuantity(updated);

        Assert.NotNull(adjustment);
        Assert.Equal(StockMovementType.Adjustment, adjustment.Type);
        Assert.Equal(stock.Id, adjustment.StockId);
        Assert.Equal(difference, adjustment.Quantity);
        Assert.Equal(initial, adjustment.PreviousQuantity);
        Assert.Equal(updated, adjustment.NewQuantity);
    }

    [Fact]
    public void UpdateQuantity_Unchanged_DoesNotRecordAdjustment()
    {
        var stock = CreateValid(quantity: 20);

        Assert.Null(stock.UpdateQuantity(20));
        Assert.Equal(20, stock.Quantity);
    }

    [Fact]
    public void UpdateQuantity_WithNegativeQuantity_ThrowsDomainExceptionWithoutModification()
    {
        var stock = CreateValid(quantity: 20);

        Assert.Throws<DomainException>(() => stock.UpdateQuantity(-1));
        Assert.Equal(20, stock.Quantity);
    }
}
