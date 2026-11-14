using Microsoft.EntityFrameworkCore;
using StockSync.Identity.Domain.Entities;
using StockSync.Identity.Domain.Repositories;

namespace StockSync.Identity.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IdentityDbContext _context;

    public UserRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = User.NormalizeEmail(email);
        return await _context.Users
            .FirstOrDefaultAsync(u => u.IsActive && u.Email.ToLower() == normalized, cancellationToken);
    }

    public async Task<User?> GetByEmailAndTenantAsync(string email, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var normalized = User.NormalizeEmail(email);
        return await _context.Users
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.IsActive && u.Email.ToLower() == normalized, cancellationToken);
    }

    public async Task<bool> ExistsByEmailAsync(string email, Guid? excludeUserId = null, CancellationToken cancellationToken = default)
    {
        var normalized = User.NormalizeEmail(email);
        var query = _context.Users
            .Where(u => u.IsActive && u.Email.ToLower() == normalized);

        if (excludeUserId.HasValue)
            query = query.Where(u => u.Id != excludeUserId.Value);

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<User> Items, int TotalCount)> ListByTenantAsync(
        Guid tenantId,
        string? search,
        RoleType? role,
        bool? isActive,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Users.Where(u => u.TenantId == tenantId);

        if (isActive.HasValue)
            query = query.Where(u => u.IsActive == isActive.Value);

        if (role.HasValue)
            query = query.Where(u => u.Role == role.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim().ToLower()}%";
            query = query.Where(u =>
                EF.Functions.ILike(u.Email, term) ||
                EF.Functions.ILike(u.FirstName, term) ||
                EF.Functions.ILike(u.LastName, term));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await _context.Users.AddAsync(user, cancellationToken);
    }
}

