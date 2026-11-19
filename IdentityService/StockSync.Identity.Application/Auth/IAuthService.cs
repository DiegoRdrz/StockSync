using StockSync.Identity.Application.Auth.DTOs;

namespace StockSync.Identity.Application.Auth;

public interface IAuthService
{
    Task<AuthResponse> RegisterTenantAsync(RegisterTenantRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<CurrentUserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

