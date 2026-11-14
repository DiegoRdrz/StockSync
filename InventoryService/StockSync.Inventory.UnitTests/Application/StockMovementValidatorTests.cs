using StockSync.Inventory.Application.Movements;

namespace StockSync.Inventory.UnitTests.Application;

public class StockMovementValidatorTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositiveQuantity_ReturnsError(int quantity) =>
        Assert.Contains("Quantity", StockMovementValidator.Validate(new StockMovementRequest(quantity)).Keys);

    [Fact]
    public void Validate_ValidData_ReturnsNoErrors()
    {
        Assert.Empty(StockMovementValidator.Validate(new StockMovementRequest(1)));
        Assert.Empty(StockMovementValidator.Validate(new StockMovementFilter()));
    }

    [Theory]
    [InlineData(0, 20, "Page")]
    [InlineData(1, 0, "PageSize")]
    [InlineData(1, 101, "PageSize")]
    [InlineData(int.MaxValue, 100, "Page")]
    public void Validate_InvalidPagination_ReturnsError(int page, int size, string field) =>
        Assert.Contains(field, StockMovementValidator.Validate(new StockMovementFilter(page, size)).Keys);

}
