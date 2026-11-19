using StockSync.Identity.Application.Tenants.DTOs;
using StockSync.Identity.Application.Users.DTOs;

namespace StockSync.Identity.Application.Auth.DTOs;

public record AuthResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    UserResponse User,
    TenantResponse Tenant);

