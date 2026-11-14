using StockSync.Identity.Domain.Entities;

namespace StockSync.Identity.Application.Users.DTOs;

public record CreateUserRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    RoleType Role = RoleType.Employee);

