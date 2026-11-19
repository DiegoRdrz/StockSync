namespace StockSync.Identity.Application.Common;

public interface ICurrentTenant
{
    Guid TenantId { get; }
}

public static class DefaultTenant
{
    public static readonly Guid Id = Guid.Parse("00000000-0000-0000-0000-000000000001");
}

