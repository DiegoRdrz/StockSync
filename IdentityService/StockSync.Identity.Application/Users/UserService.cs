using StockSync.Identity.Application.Common;
using StockSync.Identity.Application.Common.Exceptions;
using StockSync.Identity.Application.Common.Interfaces;
using StockSync.Identity.Application.Common.Models;
using StockSync.Identity.Application.Users.DTOs;
using StockSync.Identity.Domain.Entities;
using StockSync.Identity.Domain.Repositories;

namespace StockSync.Identity.Application.Users;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentTenant _currentTenant;

    public UserService(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        ICurrentTenant currentTenant)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _currentTenant = currentTenant;
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        ThrowIfErrors(UserValidator.Validate(request));

        var tenantId = _currentTenant.TenantId;
        var normalizedEmail = User.NormalizeEmail(request.Email);

        if (await _userRepository.ExistsByEmailAsync(normalizedEmail, null, cancellationToken))
            throw new ConflictException($"A user with email '{request.Email}' already exists.");

        var passwordHash = _passwordHasher.HashPassword(request.Password);

        var user = User.Create(
            tenantId,
            request.Email,
            passwordHash,
            request.FirstName,
            request.LastName,
            request.Role);

        await _userRepository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return UserResponse.From(user);
    }

    public async Task<UserResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await GetUserInCurrentTenantAsync(id, cancellationToken);
        return UserResponse.From(user);
    }

    public async Task<PagedResult<UserResponse>> ListAsync(UserFilter filter, CancellationToken cancellationToken = default)
    {
        ThrowIfErrors(UserValidator.Validate(filter));

        var tenantId = _currentTenant.TenantId;
        var skip = (filter.Page - 1) * filter.PageSize;

        var (items, total) = await _userRepository.ListByTenantAsync(
            tenantId,
            filter.Search,
            filter.Role,
            filter.IsActive,
            skip,
            filter.PageSize,
            cancellationToken);

        return new PagedResult<UserResponse>(
            items.Select(UserResponse.From).ToList(),
            filter.Page,
            filter.PageSize,
            total);
    }

    public async Task<UserResponse> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        ThrowIfErrors(UserValidator.Validate(request));

        var user = await GetUserInCurrentTenantAsync(id, cancellationToken);

        user.UpdateProfile(request.FirstName, request.LastName);
        if (request.Role.HasValue)
        {
            user.ChangeRole(request.Role.Value);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return UserResponse.From(user);
    }

    public async Task ChangePasswordAsync(Guid id, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        ThrowIfErrors(UserValidator.Validate(request));

        var user = await GetUserInCurrentTenantAsync(id, cancellationToken);

        if (!_passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
            throw new ConflictException("Current password does not match.");

        var newPasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.ChangePassword(newPasswordHash);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await GetUserInCurrentTenantAsync(id, cancellationToken);

        user.Deactivate();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<User> GetUserInCurrentTenantAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user is null || user.TenantId != _currentTenant.TenantId)
            throw new NotFoundException($"User with ID '{id}' was not found.");

        return user;
    }

    private static void ThrowIfErrors(Dictionary<string, string[]> errors)
    {
        if (errors.Count > 0)
            throw new ValidationException(errors);
    }
}

