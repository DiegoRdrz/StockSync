using System.Security.Claims;
using StockSync.Identity.Application.Common;

namespace StockSync.Identity.WebApi.MultiTenancy;

public sealed class CurrentTenantHttp : ICurrentTenant
{
    public const string ItemKey = "TenantId";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentTenantHttp(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid TenantId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is null)
                return DefaultTenant.Id;

            if (httpContext.Items[ItemKey] is Guid tenantId)
                return tenantId;

            var claimValue = httpContext.User.FindFirst("tenant_id")?.Value;
            if (claimValue is not null && Guid.TryParse(claimValue, out var claimTenantId))
                return claimTenantId;

            return DefaultTenant.Id;
        }
    }
}

