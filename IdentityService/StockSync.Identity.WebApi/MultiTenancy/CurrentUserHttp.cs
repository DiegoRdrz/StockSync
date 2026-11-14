using System.Security.Claims;
using StockSync.Identity.Application.Common;

namespace StockSync.Identity.WebApi.MultiTenancy;

public sealed class CurrentUserHttp : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserHttp(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId
    {
        get
        {
            var value = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? User?.FindFirst("sub")?.Value;

            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public Guid? TenantId
    {
        get
        {
            var value = User?.FindFirst("tenant_id")?.Value;
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? Role => User?.FindFirst(ClaimTypes.Role)?.Value;
}

