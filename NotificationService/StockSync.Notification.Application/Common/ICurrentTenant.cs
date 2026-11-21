namespace StockSync.Notification.Application.Common;

public interface ICurrentTenant
{
    Guid TenantId { get; }
}

// Tenant for data created in processes without an HTTP request (migrations, seeding and tests).
public static class DefaultTenant
{
    public static readonly Guid Id = Guid.Parse("00000000-0000-0000-0000-000000000001");
}

public sealed class TenantContext : ICurrentTenant
{
    public Guid TenantId { get; private set; } = DefaultTenant.Id;

    public void Set(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("Invalid tenant for this scope.");

        TenantId = tenantId;
    }
}
