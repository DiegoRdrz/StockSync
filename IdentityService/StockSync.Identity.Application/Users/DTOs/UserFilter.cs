using StockSync.Identity.Domain.Entities;

namespace StockSync.Identity.Application.Users.DTOs;

public record UserFilter(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    RoleType? Role = null,
    bool? IsActive = null);

