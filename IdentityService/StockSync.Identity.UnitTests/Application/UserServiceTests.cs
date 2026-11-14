using StockSync.Identity.Application.Common;
using StockSync.Identity.Application.Common.Exceptions;
using StockSync.Identity.Application.Users;
using StockSync.Identity.Application.Users.DTOs;
using StockSync.Identity.Domain.Entities;

namespace StockSync.Identity.UnitTests.Application;

public class UserServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private readonly FakeUserRepository _userRepo = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakePasswordHasher _passwordHasher = new();
    private readonly FakeCurrentTenant _currentTenant = new(TenantId);

    private IUserService CreateSut() =>
        new UserService(_userRepo, _unitOfWork, _passwordHasher, _currentTenant);

    [Fact]
    public async Task Create_WithValidData_ShouldAddUserUnderCurrentTenant()
    {
        // Arrange
        var sut = CreateSut();
        var request = new CreateUserRequest("new.user@test.com", "Password123!", "Alice", "Smith", RoleType.Employee);

        // Act
        var result = await sut.CreateAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(TenantId, result.TenantId);
        Assert.Equal("new.user@test.com", result.Email);
        Assert.Equal(RoleType.Employee, result.Role);
        Assert.True(_unitOfWork.SavedChangesCount > 0);
    }

    [Fact]
    public async Task Create_WithDuplicateEmail_ShouldThrowConflictException()
    {
        // Arrange
        var sut = CreateSut();
        var existingUser = User.Create(TenantId, "taken@test.com", "hash", "Bob", "Jones", RoleType.Employee);
        _userRepo.Users.Add(existingUser);

        var request = new CreateUserRequest("taken@test.com", "Password123!", "Charlie", "Brown", RoleType.Employee);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() => sut.CreateAsync(request));
    }

    [Fact]
    public async Task ChangePassword_WithValidCredentials_ShouldUpdatePassword()
    {
        // Arrange
        var sut = CreateSut();
        var user = User.Create(TenantId, "user@test.com", _passwordHasher.HashPassword("OldPass123!"), "Jane", "Doe", RoleType.Employee);
        _userRepo.Users.Add(user);

        var request = new ChangePasswordRequest("OldPass123!", "NewPass123!");

        // Act
        await sut.ChangePasswordAsync(user.Id, request);

        // Assert
        Assert.Equal(_passwordHasher.HashPassword("NewPass123!"), user.PasswordHash);
    }

    [Fact]
    public async Task ChangePassword_WithIncorrectCurrentPassword_ShouldThrowConflictException()
    {
        // Arrange
        var sut = CreateSut();
        var user = User.Create(TenantId, "user@test.com", _passwordHasher.HashPassword("RealPass123!"), "Jane", "Doe", RoleType.Employee);
        _userRepo.Users.Add(user);

        var request = new ChangePasswordRequest("WrongPass123!", "NewPass123!");

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() => sut.ChangePasswordAsync(user.Id, request));
    }

    [Fact]
    public async Task Deactivate_ShouldDeactivateUser()
    {
        // Arrange
        var sut = CreateSut();
        var user = User.Create(TenantId, "user@test.com", "hash", "Jane", "Doe", RoleType.Employee);
        _userRepo.Users.Add(user);

        // Act
        await sut.DeactivateAsync(user.Id);

        // Assert
        Assert.False(user.IsActive);
    }
}

public class FakeCurrentTenant : ICurrentTenant
{
    public Guid TenantId { get; }

    public FakeCurrentTenant(Guid tenantId)
    {
        TenantId = tenantId;
    }
}

