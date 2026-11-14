using StockSync.Identity.Application.Common.Models;
using StockSync.Identity.Application.Users.DTOs;

namespace StockSync.Identity.Application.Users;

public interface IUserService
{
    Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task<UserResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<UserResponse>> ListAsync(UserFilter filter, CancellationToken cancellationToken = default);
    Task<UserResponse> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(Guid id, ChangePasswordRequest request, CancellationToken cancellationToken = default);
    Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default);
}

