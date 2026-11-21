namespace StockSync.Reporting.Application.Common;

public interface ICurrentTenant
{
    Guid TenantId { get; }
}

public sealed class TenantContext : ICurrentTenant
{
    public Guid TenantId { get; private set; }

    public void Set(Guid tenantId)
    {
        if (tenantId == Guid.Empty || (TenantId != Guid.Empty && TenantId != tenantId))
            throw new InvalidOperationException("Invalid tenant for this scope.");
        TenantId = tenantId;
    }
}
