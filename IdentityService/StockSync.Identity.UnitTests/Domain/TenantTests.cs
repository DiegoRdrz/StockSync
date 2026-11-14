using StockSync.Identity.Domain.Entities;
using StockSync.Identity.Domain.Exceptions;

namespace StockSync.Identity.UnitTests.Domain;

public class TenantTests
{
    [Fact]
    public void Create_WithValidData_ShouldInitializeCorrectly()
    {
        // Act
        var tenant = Tenant.Create("Acme Corp", "TAX-12345");

        // Assert
        Assert.NotEqual(Guid.Empty, tenant.Id);
        Assert.Equal("Acme Corp", tenant.Name);
        Assert.Equal("TAX-12345", tenant.TaxId);
        Assert.True(tenant.IsActive);
        Assert.True(tenant.CreatedAt <= DateTime.UtcNow);
        Assert.Null(tenant.UpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithInvalidName_ShouldThrowDomainException(string? invalidName)
    {
        // Act & Assert
        Assert.Throws<DomainException>(() => Tenant.Create(invalidName!));
    }

    [Fact]
    public void Update_WhenActive_ShouldUpdatePropertiesAndTimestamp()
    {
        // Arrange
        var tenant = Tenant.Create("Old Name", "TAX-1");

        // Act
        tenant.Update("New Name", "TAX-2");

        // Assert
        Assert.Equal("New Name", tenant.Name);
        Assert.Equal("TAX-2", tenant.TaxId);
        Assert.NotNull(tenant.UpdatedAt);
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveToFalse()
    {
        // Arrange
        var tenant = Tenant.Create("Acme Corp");

        // Act
        tenant.Deactivate();

        // Assert
        Assert.False(tenant.IsActive);
        Assert.NotNull(tenant.UpdatedAt);
    }

    [Fact]
    public void Update_WhenInactive_ShouldThrowDomainException()
    {
        // Arrange
        var tenant = Tenant.Create("Acme Corp");
        tenant.Deactivate();

        // Act & Assert
        Assert.Throws<DomainException>(() => tenant.Update("Another Name", null));
    }
}

