using Microsoft.Extensions.Primitives;
using StockSync.Orders.Application.Common;
using StockSync.Orders.Application.Common.Exceptions;

namespace StockSync.Orders.WebApi.Multitenancy;

// The gateway must set the header after authentication; do not trust public client values.
// In Development, omission selects the default tenant.
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

    public Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api"))
            context.Items[HttpCurrentTenant.ItemKey] = Resolve(context.Request.Headers[Header]);

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
