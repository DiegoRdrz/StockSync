using StockSync.Identity.Domain.Entities;

namespace StockSync.Identity.Domain.Repositories;

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Tenant?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(string name, Guid? excludeTenantId = null, CancellationToken cancellationToken = default);
    Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default);
}

