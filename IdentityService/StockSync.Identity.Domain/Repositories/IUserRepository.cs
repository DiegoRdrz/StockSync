using StockSync.Identity.Domain.Entities;

namespace StockSync.Identity.Domain.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAndTenantAsync(string email, Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, Guid? excludeUserId = null, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<User> Items, int TotalCount)> ListByTenantAsync(
        Guid tenantId,
        string? search,
        RoleType? role,
        bool? isActive,
        int skip,
        int take,
        CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
}

