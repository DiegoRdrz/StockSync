using StockSync.Inventory.Application.Products;
using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.UnitTests.Application;

public class ProductValidatorTests
{
    private static readonly ProductRequest ValidRequest =
        new("Hammer", "FER-001", "Carpenter hammer", 10m, 15m, 5, Guid.NewGuid());

    [Fact]
    public void Validate_ValidRequest_ReturnsNoErrors()
    {
        Assert.Empty(ProductValidator.Validate(ValidRequest));
    }

    [Fact]
    public void Validate_WithoutCategory_IsValid()
    {
        Assert.Empty(ProductValidator.Validate(ValidRequest with { CategoryId = null }));
    }

    [Fact]
    public void Validate_InvalidRequest_ReturnsAllFieldErrors()
    {
        var request = new ProductRequest(
            "",
            " ",
            new string('a', Product.DescriptionMaxLength + 1),
            -1m,
            -1m,
            -1,
            Guid.Empty);

        var errors = ProductValidator.Validate(request);

        Assert.Equal(
            new[]
            {
                nameof(ProductRequest.Name),
                nameof(ProductRequest.Sku),
                nameof(ProductRequest.Description),
                nameof(ProductRequest.PurchasePrice),
                nameof(ProductRequest.SalePrice),
                nameof(ProductRequest.MinimumStock),
                nameof(ProductRequest.CategoryId)
            }.Order(),
            errors.Keys.Order());
    }

    [Fact]
    public void Validate_OverlongText_ReturnsErrors()
    {
        var request = ValidRequest with
        {
            Name = new string('a', Product.NameMaxLength + 1),
            Sku = new string('a', Product.SkuMaxLength + 1)
        };

        var errors = ProductValidator.Validate(request);

        Assert.Contains(nameof(ProductRequest.Name), errors.Keys);
        Assert.Contains(nameof(ProductRequest.Sku), errors.Keys);
    }

    [Theory]
    [InlineData("10.999")]
    [InlineData("10000000000000000")]
    public void Validate_UnrepresentablePrice_ReturnsError(string price)
    {
        var value = decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture);

        var errors = ProductValidator.Validate(ValidRequest with { PurchasePrice = value, SalePrice = value });

        Assert.Contains(nameof(ProductRequest.PurchasePrice), errors.Keys);
        Assert.Contains(nameof(ProductRequest.SalePrice), errors.Keys);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, ProductFilter.MaxPageSize)]
    public void Validate_ValidFilter_ReturnsNoErrors(int page, int pageSize)
    {
        var filter = new ProductFilter { Page = page, PageSize = pageSize };

        Assert.Empty(ProductValidator.Validate(filter));
    }

    [Theory]
    [InlineData(0, 20, nameof(ProductFilter.Page))]
    [InlineData(1, 0, nameof(ProductFilter.PageSize))]
    [InlineData(1, ProductFilter.MaxPageSize + 1, nameof(ProductFilter.PageSize))]
    [InlineData(int.MaxValue, 20, nameof(ProductFilter.Page))]
    public void Validate_InvalidFilter_ReturnsError(int page, int pageSize, string field)
    {
        var filter = new ProductFilter { Page = page, PageSize = pageSize };

        Assert.Contains(field, ProductValidator.Validate(filter).Keys);
    }
}
