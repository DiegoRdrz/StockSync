using Microsoft.EntityFrameworkCore;
using StockSync.Identity.Domain.Entities;
using StockSync.Identity.Domain.Repositories;

namespace StockSync.Identity.Infrastructure.Repositories;

public class TenantRepository : ITenantRepository
{
    private readonly IdentityDbContext _context;

    public TenantRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Tenants
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<Tenant?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalized = Tenant.NormalizeName(name);
        return await _context.Tenants
            .FirstOrDefaultAsync(t => t.IsActive && t.Name.ToUpper() == normalized, cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(string name, Guid? excludeTenantId = null, CancellationToken cancellationToken = default)
    {
        var normalized = Tenant.NormalizeName(name);
        var query = _context.Tenants
            .Where(t => t.IsActive && t.Name.ToUpper() == normalized);

        if (excludeTenantId.HasValue)
            query = query.Where(t => t.Id != excludeTenantId.Value);

        return await query.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        await _context.Tenants.AddAsync(tenant, cancellationToken);
    }
}

