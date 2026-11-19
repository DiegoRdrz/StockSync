using StockSync.Identity.Domain.Entities;

namespace StockSync.Identity.Application.Users.DTOs;

public record UserResponse(
    Guid Id,
    Guid TenantId,
    string Email,
    string FirstName,
    string LastName,
    RoleType Role,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public static UserResponse From(User user) =>
        new(user.Id, user.TenantId, user.Email, user.FirstName, user.LastName, user.Role, user.IsActive, user.CreatedAt, user.UpdatedAt);
}

