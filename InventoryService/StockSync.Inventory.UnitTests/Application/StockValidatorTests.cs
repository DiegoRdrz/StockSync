using StockSync.Inventory.Application.Stocks;

namespace StockSync.Inventory.UnitTests.Application;

public class StockValidatorTests
{
    private static readonly StockRequest ValidRequest = new(Guid.NewGuid(), Guid.NewGuid(), 20);

    [Fact]
    public void Validate_ValidRequest_ReturnsNoErrors()
    {
        Assert.Empty(StockValidator.Validate(ValidRequest));
    }

    [Fact]
    public void Validate_ZeroQuantity_IsValid()
    {
        Assert.Empty(StockValidator.Validate(ValidRequest with { Quantity = 0 }));
    }

    [Fact]
    public void Validate_InvalidRequest_ReturnsAllFieldErrors()
    {
        var request = new StockRequest(Guid.Empty, Guid.Empty, -1);

        var errors = StockValidator.Validate(request);

        Assert.Equal(
            new[]
            {
                nameof(StockRequest.ProductId),
                nameof(StockRequest.BranchId),
                nameof(StockRequest.Quantity)
            }.Order(),
            errors.Keys.Order());
    }

    [Fact]
    public void Validate_ValidQuantityRequest_ReturnsNoErrors()
    {
        Assert.Empty(StockValidator.Validate(new StockQuantityRequest(0)));
        Assert.Empty(StockValidator.Validate(new StockQuantityRequest(10)));
    }

    [Theory]
    [InlineData(0, 20, nameof(StockFilter.Page))]
    [InlineData(1, 0, nameof(StockFilter.PageSize))]
    [InlineData(1, StockFilter.MaxPageSize + 1, nameof(StockFilter.PageSize))]
    [InlineData(int.MaxValue, 20, nameof(StockFilter.Page))]
    public void Validate_InvalidFilter_ReturnsError(int page, int pageSize, string field)
    {
        var filter = new LowStockFilter { Page = page, PageSize = pageSize };

        Assert.Contains(field, StockValidator.Validate(filter).Keys);
    }

    [Fact]
    public void Validate_DefaultFilter_ReturnsNoErrors()
    {
        Assert.Empty(StockValidator.Validate(new StockFilter()));
    }

    [Fact]
    public void Validate_NegativeQuantityRequest_ReturnsError()
    {
        var errors = StockValidator.Validate(new StockQuantityRequest(-1));

        Assert.Contains(nameof(StockQuantityRequest.NewQuantity), errors.Keys);
    }
}
