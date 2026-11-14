using StockSync.Identity.Application.Tenants.DTOs;
using StockSync.Identity.Application.Users.DTOs;

namespace StockSync.Identity.Application.Auth.DTOs;

public record CurrentUserResponse(
    UserResponse User,
    TenantResponse Tenant);

