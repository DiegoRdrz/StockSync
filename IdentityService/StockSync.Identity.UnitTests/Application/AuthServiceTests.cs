using StockSync.Identity.Application.Auth;
using StockSync.Identity.Application.Auth.DTOs;
using StockSync.Identity.Application.Common.Exceptions;
using StockSync.Identity.Application.Common.Interfaces;
using StockSync.Identity.Domain.Entities;
using StockSync.Identity.Domain.Exceptions;
using StockSync.Identity.Domain.Repositories;

namespace StockSync.Identity.UnitTests.Application;

public class AuthServiceTests
{
    private readonly FakeTenantRepository _tenantRepo = new();
    private readonly FakeUserRepository _userRepo = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakePasswordHasher _passwordHasher = new();
    private readonly FakeJwtTokenGenerator _tokenGenerator = new();

    private IAuthService CreateSut() =>
        new AuthService(_tenantRepo, _userRepo, _unitOfWork, _passwordHasher, _tokenGenerator);

    [Fact]
    public async Task RegisterTenant_WithValidData_ShouldCreateTenantAndOwner()
    {
        // Arrange
        var sut = CreateSut();
        var request = new RegisterTenantRequest(
            "Acme Hardware",
            "TAX-1234",
            "John",
            "Owner",
            "owner@acme.com",
            "SecurePassword123!");

        // Act
        var result = await sut.RegisterTenantAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Bearer", result.TokenType);
        Assert.Equal("fake_jwt_token", result.AccessToken);
        Assert.Equal("Acme Hardware", result.Tenant.Name);
        Assert.Equal("owner@acme.com", result.User.Email);
        Assert.Equal(RoleType.Owner, result.User.Role);
        Assert.True(_unitOfWork.SavedChangesCount > 0);
    }

    [Fact]
    public async Task RegisterTenant_WithDuplicateEmail_ShouldThrowConflictException()
    {
        // Arrange
        var sut = CreateSut();
        var tenant = Tenant.Create("Existing Tenant");
        _tenantRepo.Tenants.Add(tenant);
        var existingUser = User.Create(tenant.Id, "existing@test.com", "hash", "A", "B", RoleType.Owner);
        _userRepo.Users.Add(existingUser);

        var request = new RegisterTenantRequest(
            "New Company",
            null,
            "John",
            "Doe",
            "existing@test.com",
            "Password123!");

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() => sut.RegisterTenantAsync(request));
    }

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnAuthResponse()
    {
        // Arrange
        var sut = CreateSut();
        var tenant = Tenant.Create("Test Tenant");
        _tenantRepo.Tenants.Add(tenant);

        var password = "Password123!";
        var passwordHash = _passwordHasher.HashPassword(password);
        var user = User.Create(tenant.Id, "user@test.com", passwordHash, "Jane", "Doe", RoleType.Employee);
        _userRepo.Users.Add(user);

        var request = new LoginRequest("user@test.com", password);

        // Act
        var result = await sut.LoginAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("fake_jwt_token", result.AccessToken);
        Assert.Equal("user@test.com", result.User.Email);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ShouldThrowInvalidCredentialsException()
    {
        // Arrange
        var sut = CreateSut();
        var tenant = Tenant.Create("Test Tenant");
        _tenantRepo.Tenants.Add(tenant);

        var user = User.Create(tenant.Id, "user@test.com", _passwordHasher.HashPassword("Correct123!"), "Jane", "Doe", RoleType.Employee);
        _userRepo.Users.Add(user);

        var request = new LoginRequest("user@test.com", "WrongPassword!");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCredentialsException>(() => sut.LoginAsync(request));
    }

    [Fact]
    public async Task Login_WithNonExistentUser_ShouldThrowInvalidCredentialsException()
    {
        // Arrange
        var sut = CreateSut();
        var request = new LoginRequest("nonexistent@test.com", "Password123!");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCredentialsException>(() => sut.LoginAsync(request));
    }
}

public class FakeTenantRepository : ITenantRepository
{
    public List<Tenant> Tenants { get; } = [];

    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Tenants.FirstOrDefault(t => t.Id == id));

    public Task<Tenant?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult(Tenants.FirstOrDefault(t => t.IsActive && t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));

    public Task<bool> ExistsByNameAsync(string name, Guid? excludeTenantId = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(Tenants.Any(t => t.IsActive && (!excludeTenantId.HasValue || t.Id != excludeTenantId) && t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));

    public Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        Tenants.Add(tenant);
        return Task.CompletedTask;
    }
}

public class FakeUserRepository : IUserRepository
{
    public List<User> Users { get; } = [];

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.FirstOrDefault(u => u.IsActive && u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));

    public Task<User?> GetByEmailAndTenantAsync(string email, Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.FirstOrDefault(u => u.TenantId == tenantId && u.IsActive && u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));

    public Task<bool> ExistsByEmailAsync(string email, Guid? excludeUserId = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.Any(u => u.IsActive && (!excludeUserId.HasValue || u.Id != excludeUserId) && u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));

    public Task<(IReadOnlyList<User> Items, int TotalCount)> ListByTenantAsync(Guid tenantId, string? search, RoleType? role, bool? isActive, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = Users.Where(u => u.TenantId == tenantId);
        if (isActive.HasValue) query = query.Where(u => u.IsActive == isActive.Value);
        if (role.HasValue) query = query.Where(u => u.Role == role.Value);

        var list = query.ToList();
        return Task.FromResult< (IReadOnlyList<User>, int)>((list.Skip(skip).Take(take).ToList(), list.Count));
    }

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        Users.Add(user);
        return Task.CompletedTask;
    }
}

public class FakeUnitOfWork : IUnitOfWork
{
    public int SavedChangesCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SavedChangesCount++;
        return Task.FromResult(1);
    }
}

public class FakePasswordHasher : IPasswordHasher
{
    public string HashPassword(string password) => $"hash_{password}";
    public bool VerifyPassword(string password, string passwordHash) => passwordHash == $"hash_{password}";
}

public class FakeJwtTokenGenerator : IJwtTokenGenerator
{
    public int ExpirationMinutes => 60;
    public string GenerateToken(User user, Tenant tenant) => "fake_jwt_token";
}

