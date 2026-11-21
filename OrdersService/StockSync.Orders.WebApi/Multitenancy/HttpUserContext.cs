using StockSync.Orders.Application.Common;

namespace StockSync.Orders.WebApi.Multitenancy;

public sealed class HttpUserContext : IUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? AuthorizationHeader =>
        _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
}
