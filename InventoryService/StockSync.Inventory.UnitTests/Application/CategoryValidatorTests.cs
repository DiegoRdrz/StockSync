using StockSync.Inventory.Application.Categories;

namespace StockSync.Inventory.UnitTests.Application;

public class CategoryValidatorTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, CategoryFilter.MaxPageSize)]
    public void Validate_ValidFilter_ReturnsNoErrors(int page, int pageSize)
    {
        var filter = new CategoryFilter { Page = page, PageSize = pageSize };

        Assert.Empty(CategoryValidator.Validate(filter));
    }

    [Theory]
    [InlineData(0, 20, nameof(CategoryFilter.Page))]
    [InlineData(1, 0, nameof(CategoryFilter.PageSize))]
    [InlineData(1, CategoryFilter.MaxPageSize + 1, nameof(CategoryFilter.PageSize))]
    [InlineData(int.MaxValue, 20, nameof(CategoryFilter.Page))]
    public void Validate_InvalidFilter_ReturnsError(int page, int pageSize, string field)
    {
        var filter = new CategoryFilter { Page = page, PageSize = pageSize };

        Assert.Contains(field, CategoryValidator.Validate(filter).Keys);
    }
}
