using StockSync.Identity.Domain.Entities;
using StockSync.Identity.Domain.Exceptions;

namespace StockSync.Identity.UnitTests.Domain;

public class UserTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidData_ShouldInitializeCorrectly()
    {
        // Act
        var user = User.Create(
            TenantId,
            "john.doe@example.com",
            "hashed_password_123",
            "John",
            "Doe",
            RoleType.Owner);

        // Assert
        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal(TenantId, user.TenantId);
        Assert.Equal("john.doe@example.com", user.Email);
        Assert.Equal("hashed_password_123", user.PasswordHash);
        Assert.Equal("John", user.FirstName);
        Assert.Equal("Doe", user.LastName);
        Assert.Equal(RoleType.Owner, user.Role);
        Assert.True(user.IsActive);
        Assert.Null(user.UpdatedAt);
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("missingat.com")]
    [InlineData("@nodomain")]
    [InlineData("")]
    public void Create_WithInvalidEmail_ShouldThrowDomainException(string invalidEmail)
    {
        // Act & Assert
        Assert.Throws<DomainException>(() => User.Create(
            TenantId,
            invalidEmail,
            "hashed",
            "John",
            "Doe",
            RoleType.Employee));
    }

    [Fact]
    public void ChangePassword_ShouldUpdatePasswordHashAndTimestamp()
    {
        // Arrange
        var user = User.Create(TenantId, "user@test.com", "old_hash", "Jane", "Doe", RoleType.Employee);

        // Act
        user.ChangePassword("new_hash_456");

        // Assert
        Assert.Equal("new_hash_456", user.PasswordHash);
        Assert.NotNull(user.UpdatedAt);
    }

    [Fact]
    public void Deactivate_ShouldMarkUserAsInactive()
    {
        // Arrange
        var user = User.Create(TenantId, "user@test.com", "old_hash", "Jane", "Doe", RoleType.Employee);

        // Act
        user.Deactivate();

        // Assert
        Assert.False(user.IsActive);
        Assert.NotNull(user.UpdatedAt);
    }

    [Fact]
    public void UpdateProfile_WhenInactive_ShouldThrowDomainException()
    {
        // Arrange
        var user = User.Create(TenantId, "user@test.com", "old_hash", "Jane", "Doe", RoleType.Employee);
        user.Deactivate();

        // Act & Assert
        Assert.Throws<DomainException>(() => user.UpdateProfile("Updated", "Name"));
    }
}

