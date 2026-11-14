using StockSync.Inventory.Application.Common;

namespace StockSync.Inventory.WebApi.Multitenancy;

public sealed class HttpCurrentTenant : ICurrentTenant
{
    public const string ItemKey = "TenantId";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentTenant(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // Use the default tenant outside HTTP requests, including startup migrations and seeding.
    public Guid TenantId => _httpContextAccessor.HttpContext?.Items[ItemKey] is Guid tenantId
        ? tenantId
        : DefaultTenant.Id;
}
