using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Exceptions;

namespace StockSync.Inventory.UnitTests.Domain;

public class ProductTests
{
    private static Product CreateValid(
        string name = "Hammer",
        string sku = "FER-001",
        string? description = "Carpenter hammer",
        decimal purchasePrice = 10m,
        decimal salePrice = 15m,
        int minimumStock = 5,
        Guid? categoryId = null) =>
        Product.Create(name, sku, description, purchasePrice, salePrice, minimumStock, categoryId);

    [Fact]
    public void Create_WithValidData_InitializesActiveProduct()
    {
        var categoryId = Guid.NewGuid();

        var product = CreateValid(categoryId: categoryId);

        Assert.NotEqual(Guid.Empty, product.Id);
        Assert.True(product.IsActive);
        Assert.Equal("Hammer", product.Name);
        Assert.Equal(categoryId, product.CategoryId);
        Assert.Null(product.UpdatedAt);
        Assert.Equal(DateTimeKind.Utc, product.CreatedAt.Kind);
    }

    [Fact]
    public void Create_NormalizesSkuAndTrimsText()
    {
        var product = CreateValid(name: "  Hammer  ", sku: " fer-001 ", description: "   ");

        Assert.Equal("Hammer", product.Name);
        Assert.Equal("FER-001", product.Sku);
        Assert.Null(product.Description);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutName_ThrowsDomainException(string name)
    {
        Assert.Throws<DomainException>(() => CreateValid(name: name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutSku_ThrowsDomainException(string sku)
    {
        Assert.Throws<DomainException>(() => CreateValid(sku: sku));
    }

    [Fact]
    public void Create_WithOverlongText_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => CreateValid(name: new string('a', Product.NameMaxLength + 1)));
        Assert.Throws<DomainException>(() => CreateValid(sku: new string('a', Product.SkuMaxLength + 1)));
        Assert.Throws<DomainException>(() => CreateValid(description: new string('a', Product.DescriptionMaxLength + 1)));
    }

    [Fact]
    public void Create_WithNegativeValues_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => CreateValid(purchasePrice: -0.01m));
        Assert.Throws<DomainException>(() => CreateValid(salePrice: -1m));
        Assert.Throws<DomainException>(() => CreateValid(minimumStock: -1));
    }

    [Theory]
    [InlineData("10.999")]
    [InlineData("10000000000000000")]
    public void Create_WithUnrepresentablePrice_ThrowsDomainException(string price)
    {
        var value = decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Throws<DomainException>(() => CreateValid(purchasePrice: value));
        Assert.Throws<DomainException>(() => CreateValid(salePrice: value));
    }

    [Fact]
    public void Create_WithMaximumPriceAndTwoDecimals_IsValid()
    {
        var product = CreateValid(purchasePrice: 10.50m, salePrice: Product.MaxPrice);

        Assert.Equal(Product.MaxPrice, product.SalePrice);
    }

    [Fact]
    public void Create_WithZeroPricesAndStock_IsValid()
    {
        var product = CreateValid(purchasePrice: 0m, salePrice: 0m, minimumStock: 0);

        Assert.Equal(0m, product.SalePrice);
    }

    [Fact]
    public void Create_WithEmptyCategory_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => CreateValid(categoryId: Guid.Empty));
    }

    [Fact]
    public void Update_UpdatesDetailsAndTimestamp()
    {
        var product = CreateValid();

        product.Update("Large hammer", "fer-002", null, 20m, 30m, 2, null);

        Assert.Equal("Large hammer", product.Name);
        Assert.Equal("FER-002", product.Sku);
        Assert.Equal(30m, product.SalePrice);
        Assert.NotNull(product.UpdatedAt);
    }

    [Fact]
    public void Update_WithInvalidData_DoesNotModifyProduct()
    {
        var product = CreateValid();

        Assert.Throws<DomainException>(() => product.Update("Other", "FER-001", null, -5m, 15m, 5, null));
        Assert.Equal("Hammer", product.Name);
        Assert.Equal(10m, product.PurchasePrice);
    }

    // Repeated iterations prevent exact-microsecond UtcNow values from masking a failure.
    [Fact]
    public void Dates_AreTruncatedToPostgreSqlMicroseconds()
    {
        for (var i = 0; i < 20; i++)
        {
            var product = CreateValid();
            product.Update("Large hammer", "FER-001", null, 1m, 2m, 0, null);

            Assert.Equal(0, product.CreatedAt.Ticks % TimeSpan.TicksPerMicrosecond);
            Assert.Equal(0, product.UpdatedAt!.Value.Ticks % TimeSpan.TicksPerMicrosecond);
            Assert.Equal(DateTimeKind.Utc, product.UpdatedAt.Value.Kind);
        }
    }

    [Fact]
    public void Deactivate_MarksInactive()
    {
        var product = CreateValid();

        product.Deactivate();

        Assert.False(product.IsActive);
        Assert.NotNull(product.UpdatedAt);
    }

    [Fact]
    public void Deactivate_AlreadyInactiveProduct_ThrowsDomainException()
    {
        var product = CreateValid();
        product.Deactivate();

        Assert.Throws<DomainException>(product.Deactivate);
    }

    [Fact]
    public void Update_InactiveProduct_ThrowsDomainException()
    {
        var product = CreateValid();
        product.Deactivate();

        Assert.Throws<DomainException>(() => product.Update("Other", "FER-001", null, 1m, 2m, 0, null));
    }
}
