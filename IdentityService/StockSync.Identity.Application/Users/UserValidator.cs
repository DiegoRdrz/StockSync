using StockSync.Identity.Application.Auth;
using StockSync.Identity.Application.Users.DTOs;
using StockSync.Identity.Domain.Entities;

namespace StockSync.Identity.Application.Users;

public static class UserValidator
{
    public const int MaxPageSize = 100;

    public static Dictionary<string, string[]> Validate(CreateUserRequest request)
    {
        var errors = new Dictionary<string, List<string>>();

        if (string.IsNullOrWhiteSpace(request.FirstName))
            Add(errors, nameof(request.FirstName), "First name is required.");
        else if (request.FirstName.Trim().Length > User.FirstNameMaxLength)
            Add(errors, nameof(request.FirstName), $"First name cannot exceed {User.FirstNameMaxLength} characters.");

        if (string.IsNullOrWhiteSpace(request.LastName))
            Add(errors, nameof(request.LastName), "Last name is required.");
        else if (request.LastName.Trim().Length > User.LastNameMaxLength)
            Add(errors, nameof(request.LastName), $"Last name cannot exceed {User.LastNameMaxLength} characters.");

        AuthValidator.ValidateEmail(errors, nameof(request.Email), request.Email);
        AuthValidator.ValidatePassword(errors, nameof(request.Password), request.Password);

        if (!Enum.IsDefined(typeof(RoleType), request.Role))
            Add(errors, nameof(request.Role), "A valid role must be selected.");

        return ToResult(errors);
    }

    public static Dictionary<string, string[]> Validate(UpdateUserRequest request)
    {
        var errors = new Dictionary<string, List<string>>();

        if (string.IsNullOrWhiteSpace(request.FirstName))
            Add(errors, nameof(request.FirstName), "First name is required.");
        else if (request.FirstName.Trim().Length > User.FirstNameMaxLength)
            Add(errors, nameof(request.FirstName), $"First name cannot exceed {User.FirstNameMaxLength} characters.");

        if (string.IsNullOrWhiteSpace(request.LastName))
            Add(errors, nameof(request.LastName), "Last name is required.");
        else if (request.LastName.Trim().Length > User.LastNameMaxLength)
            Add(errors, nameof(request.LastName), $"Last name cannot exceed {User.LastNameMaxLength} characters.");

        if (request.Role.HasValue && !Enum.IsDefined(typeof(RoleType), request.Role.Value))
            Add(errors, nameof(request.Role), "A valid role must be selected.");

        return ToResult(errors);
    }

    public static Dictionary<string, string[]> Validate(ChangePasswordRequest request)
    {
        var errors = new Dictionary<string, List<string>>();

        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
            Add(errors, nameof(request.CurrentPassword), "Current password is required.");

        AuthValidator.ValidatePassword(errors, nameof(request.NewPassword), request.NewPassword);

        if (request.CurrentPassword == request.NewPassword)
            Add(errors, nameof(request.NewPassword), "New password cannot be the same as the current password.");

        return ToResult(errors);
    }

    public static Dictionary<string, string[]> Validate(UserFilter filter)
    {
        var errors = new Dictionary<string, List<string>>();

        if (filter.Page < 1)
            Add(errors, nameof(filter.Page), "Page must be greater than or equal to 1.");

        if (filter.PageSize < 1 || filter.PageSize > MaxPageSize)
            Add(errors, nameof(filter.PageSize), $"Page size must be between 1 and {MaxPageSize}.");

        if (filter.Page > 0 && filter.PageSize > 0 && (long)(filter.Page - 1) * filter.PageSize > int.MaxValue)
            Add(errors, nameof(filter.Page), "Requested page offset exceeds allowed limits.");

        if (filter.Role.HasValue && !Enum.IsDefined(typeof(RoleType), filter.Role.Value))
            Add(errors, nameof(filter.Role), "Invalid role filter.");

        return ToResult(errors);
    }

    private static void Add(Dictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var messages))
            errors[field] = messages = [];
        messages.Add(message);
    }

    private static Dictionary<string, string[]> ToResult(Dictionary<string, List<string>> errors) =>
        errors.ToDictionary(e => e.Key, e => e.Value.ToArray());
}

