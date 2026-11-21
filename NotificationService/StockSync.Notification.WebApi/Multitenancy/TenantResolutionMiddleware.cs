using Microsoft.Extensions.Primitives;
using StockSync.Notification.Application.Common;
using StockSync.Notification.Application.Common.Exceptions;

namespace StockSync.Notification.WebApi.Multitenancy;

// The gateway sets the header from the authenticated user's JWT claim;
// in Development, omission selects the default tenant.
public sealed class TenantResolutionMiddleware
{
    public const string Header = "X-Tenant-Id";

    private readonly RequestDelegate _next;
    private readonly bool _requireHeader;

    public TenantResolutionMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _requireHeader = configuration.GetValue("Multitenancy:RequireHeader", true);
    }

    public Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        if (context.Request.Path.StartsWithSegments("/api"))
            tenantContext.Set(Resolve(context.Request.Headers[Header]));

        return _next(context);
    }

    private Guid Resolve(StringValues value)
    {
        if (StringValues.IsNullOrEmpty(value))
            return _requireHeader
                ? throw Error($"Header {Header} is required.")
                : DefaultTenant.Id;

        if (!Guid.TryParse(value, out var tenantId) || tenantId == Guid.Empty)
            throw Error($"Header {Header} must be a valid GUID.");

        return tenantId;
    }

    private static ValidationException Error(string message) =>
        new(new Dictionary<string, string[]> { [Header] = [message] });
}
