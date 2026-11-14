using StockSync.Identity.Application.Auth.DTOs;
using StockSync.Identity.Domain.Entities;

namespace StockSync.Identity.Application.Auth;

public static class AuthValidator
{
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 100;

    public static Dictionary<string, string[]> Validate(RegisterTenantRequest request)
    {
        var errors = new Dictionary<string, List<string>>();

        if (string.IsNullOrWhiteSpace(request.CompanyName))
            Add(errors, nameof(request.CompanyName), "Company name is required.");
        else if (request.CompanyName.Trim().Length > Tenant.NameMaxLength)
            Add(errors, nameof(request.CompanyName), $"Company name cannot exceed {Tenant.NameMaxLength} characters.");

        if (request.TaxId?.Trim().Length > Tenant.TaxIdMaxLength)
            Add(errors, nameof(request.TaxId), $"Tax ID cannot exceed {Tenant.TaxIdMaxLength} characters.");

        if (string.IsNullOrWhiteSpace(request.FirstName))
            Add(errors, nameof(request.FirstName), "First name is required.");
        else if (request.FirstName.Trim().Length > User.FirstNameMaxLength)
            Add(errors, nameof(request.FirstName), $"First name cannot exceed {User.FirstNameMaxLength} characters.");

        if (string.IsNullOrWhiteSpace(request.LastName))
            Add(errors, nameof(request.LastName), "Last name is required.");
        else if (request.LastName.Trim().Length > User.LastNameMaxLength)
            Add(errors, nameof(request.LastName), $"Last name cannot exceed {User.LastNameMaxLength} characters.");

        ValidateEmail(errors, nameof(request.Email), request.Email);
        ValidatePassword(errors, nameof(request.Password), request.Password);

        return ToResult(errors);
    }

    public static Dictionary<string, string[]> Validate(LoginRequest request)
    {
        var errors = new Dictionary<string, List<string>>();

        if (string.IsNullOrWhiteSpace(request.Email))
            Add(errors, nameof(request.Email), "Email is required.");

        if (string.IsNullOrWhiteSpace(request.Password))
            Add(errors, nameof(request.Password), "Password is required.");

        return ToResult(errors);
    }

    public static void ValidateEmail(Dictionary<string, List<string>> errors, string propertyName, string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            Add(errors, propertyName, "Email is required.");
            return;
        }

        var trimmed = email.Trim();
        if (trimmed.Length > User.EmailMaxLength)
            Add(errors, propertyName, $"Email cannot exceed {User.EmailMaxLength} characters.");

        if (!trimmed.Contains('@') || !trimmed.Contains('.'))
            Add(errors, propertyName, "A valid email address is required.");
    }

    public static void ValidatePassword(Dictionary<string, List<string>> errors, string propertyName, string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            Add(errors, propertyName, "Password is required.");
            return;
        }

        if (password.Length < MinPasswordLength)
            Add(errors, propertyName, $"Password must be at least {MinPasswordLength} characters long.");

        if (password.Length > MaxPasswordLength)
            Add(errors, propertyName, $"Password cannot exceed {MaxPasswordLength} characters.");

        if (!password.Any(char.IsDigit))
            Add(errors, propertyName, "Password must contain at least one digit.");

        if (!password.Any(char.IsLetter))
            Add(errors, propertyName, "Password must contain at least one letter.");
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

