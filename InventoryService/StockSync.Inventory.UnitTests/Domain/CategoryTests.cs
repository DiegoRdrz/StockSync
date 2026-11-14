using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Exceptions;

namespace StockSync.Inventory.UnitTests.Domain;

public class CategoryTests
{
    [Fact]
    public void Create_WithValidData_InitializesActiveCategory()
    {
        var category = Category.Create("Tools", "General-purpose tools");

        Assert.NotEqual(Guid.Empty, category.Id);
        Assert.True(category.IsActive);
        Assert.Equal("Tools", category.Name);
        Assert.Equal("General-purpose tools", category.Description);
        Assert.Null(category.UpdatedAt);
    }

    [Fact]
    public void Create_NormalizesDataAndConvertsEmptyTextToNull()
    {
        var category = Category.Create(" Tools ", "   ");

        Assert.Equal("Tools", category.Name);
        Assert.Equal("TOOLS", category.NormalizedName);
        Assert.Null(category.Description);
    }

    [Fact]
    public void Update_RecalculatesNormalizedName()
    {
        var category = Category.Create("Tools", null);

        category.Update(" Stationery ", null);

        Assert.Equal("STATIONERY", category.NormalizedName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutName_ThrowsDomainException(string name)
    {
        Assert.Throws<DomainException>(() => Category.Create(name, null));
    }

    [Fact]
    public void Create_WithOverlongText_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => Category.Create(new string('a', Category.NameMaxLength + 1), null));
        Assert.Throws<DomainException>(() => Category.Create("Valid", new string('a', Category.DescriptionMaxLength + 1)));
    }

    [Fact]
    public void Update_UpdatesDetailsAndTimestamp()
    {
        var category = Category.Create("Tools", null);

        category.Update("Other tools", "New description");

        Assert.Equal("Other tools", category.Name);
        Assert.Equal("New description", category.Description);
        Assert.NotNull(category.UpdatedAt);
    }

    // Repeated iterations prevent exact-microsecond UtcNow values from masking a failure.
    [Fact]
    public void Dates_AreTruncatedToPostgreSqlMicroseconds()
    {
        for (var i = 0; i < 20; i++)
        {
            var category = Category.Create("Tools", null);
            category.Deactivate();

            Assert.Equal(0, category.CreatedAt.Ticks % TimeSpan.TicksPerMicrosecond);
            Assert.Equal(0, category.UpdatedAt!.Value.Ticks % TimeSpan.TicksPerMicrosecond);
            Assert.Equal(DateTimeKind.Utc, category.CreatedAt.Kind);
        }
    }

    [Fact]
    public void Deactivate_MarksInactiveAndUpdatesTimestamp()
    {
        var category = Category.Create("Test", null);

        category.Deactivate();

        Assert.False(category.IsActive);
        Assert.NotNull(category.UpdatedAt);
    }

    [Fact]
    public void UpdateOrDeactivate_Inactive_ThrowsDomainException()
    {
        var category = Category.Create("Test", null);
        category.Deactivate();

        Assert.Throws<DomainException>(() => category.Update("Test2", null));
        Assert.Throws<DomainException>(category.Deactivate);
    }
}
