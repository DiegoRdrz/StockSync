using StockSync.Identity.Domain.Entities;

namespace StockSync.Identity.Application.Users.DTOs;

public record UpdateUserRequest(
    string FirstName,
    string LastName,
    RoleType? Role);

