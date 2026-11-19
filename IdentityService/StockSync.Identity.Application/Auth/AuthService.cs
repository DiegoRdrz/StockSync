using StockSync.Identity.Application.Auth.DTOs;
using StockSync.Identity.Application.Common.Exceptions;
using StockSync.Identity.Application.Common.Interfaces;
using StockSync.Identity.Application.Tenants.DTOs;
using StockSync.Identity.Application.Users.DTOs;
using StockSync.Identity.Domain.Entities;
using StockSync.Identity.Domain.Exceptions;
using StockSync.Identity.Domain.Repositories;

namespace StockSync.Identity.Application.Auth;

public class AuthService : IAuthService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public AuthService(
        ITenantRepository tenantRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _tenantRepository = tenantRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<AuthResponse> RegisterTenantAsync(RegisterTenantRequest request, CancellationToken cancellationToken = default)
    {
        ThrowIfErrors(AuthValidator.Validate(request));

        var normalizedEmail = User.NormalizeEmail(request.Email);
        if (await _userRepository.ExistsByEmailAsync(normalizedEmail, null, cancellationToken))
            throw new ConflictException($"A user with email '{request.Email}' already exists.");

        if (await _tenantRepository.ExistsByNameAsync(request.CompanyName, null, cancellationToken))
            throw new ConflictException($"A company with name '{request.CompanyName}' already exists.");

        var tenant = Tenant.Create(request.CompanyName, request.TaxId);
        var passwordHash = _passwordHasher.HashPassword(request.Password);

        var ownerUser = User.Create(
            tenant.Id,
            request.Email,
            passwordHash,
            request.FirstName,
            request.LastName,
            RoleType.Owner);

        await _tenantRepository.AddAsync(tenant, cancellationToken);
        await _userRepository.AddAsync(ownerUser, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var token = _jwtTokenGenerator.GenerateToken(ownerUser, tenant);

        return new AuthResponse(
            token,
            "Bearer",
            _jwtTokenGenerator.ExpirationMinutes * 60,
            UserResponse.From(ownerUser),
            TenantResponse.From(tenant));
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        ThrowIfErrors(AuthValidator.Validate(request));

        var normalizedEmail = User.NormalizeEmail(request.Email);
        var user = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken)
            ?? throw new InvalidCredentialsException();

        if (!user.IsActive)
            throw new InvalidCredentialsException("User account is inactive. Please contact your company administrator.");

        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
            throw new InvalidCredentialsException();

        var tenant = await _tenantRepository.GetByIdAsync(user.TenantId, cancellationToken)
            ?? throw new ConflictException("User tenant was not found or is no longer accessible.");

        if (!tenant.IsActive)
            throw new InvalidCredentialsException("Tenant organization is inactive. Please contact support.");

        var token = _jwtTokenGenerator.GenerateToken(user, tenant);

        return new AuthResponse(
            token,
            "Bearer",
            _jwtTokenGenerator.ExpirationMinutes * 60,
            UserResponse.From(user),
            TenantResponse.From(tenant));
    }

    public async Task<CurrentUserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"User with ID '{userId}' was not found.");

        var tenant = await _tenantRepository.GetByIdAsync(user.TenantId, cancellationToken)
            ?? throw new NotFoundException($"Tenant with ID '{user.TenantId}' was not found.");

        return new CurrentUserResponse(
            UserResponse.From(user),
            TenantResponse.From(tenant));
    }

    private static void ThrowIfErrors(Dictionary<string, string[]> errors)
    {
        if (errors.Count > 0)
            throw new ValidationException(errors);
    }
}

