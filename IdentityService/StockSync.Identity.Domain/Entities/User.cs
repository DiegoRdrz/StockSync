using StockSync.Identity.Domain.Common;
using StockSync.Identity.Domain.Exceptions;

namespace StockSync.Identity.Domain.Entities;

public class User
{
    public const int FirstNameMaxLength = 100;
    public const int LastNameMaxLength = 100;
    public const int EmailMaxLength = 200;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public RoleType Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private User()
    {
    }

    public static User Create(
        Guid tenantId,
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        RoleType role,
        Guid? id = null)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("Tenant ID is required.");

        var user = new User
        {
            Id = id ?? Guid.NewGuid(),
            TenantId = tenantId,
            IsActive = true,
            CreatedAt = ClockUtc.Now()
        };

        user.SetEmail(email);
        user.SetPasswordHash(passwordHash);
        user.SetProfile(firstName, lastName);
        user.Role = role;

        return user;
    }

    public void UpdateProfile(string firstName, string lastName)
    {
        EnsureActive();
        SetProfile(firstName, lastName);
        UpdatedAt = ClockUtc.Now();
    }

    public void ChangePassword(string newPasswordHash)
    {
        EnsureActive();
        SetPasswordHash(newPasswordHash);
        UpdatedAt = ClockUtc.Now();
    }

    public void ChangeRole(RoleType newRole)
    {
        EnsureActive();
        Role = newRole;
        UpdatedAt = ClockUtc.Now();
    }

    public void Deactivate()
    {
        EnsureActive();
        IsActive = false;
        UpdatedAt = ClockUtc.Now();
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private void SetEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("Email is required.");

        var trimmedEmail = email.Trim();
        if (trimmedEmail.Length > EmailMaxLength)
            throw new DomainException($"Email cannot exceed {EmailMaxLength} characters.");

        if (!trimmedEmail.Contains('@') || !trimmedEmail.Contains('.'))
            throw new DomainException("Invalid email format.");

        Email = NormalizeEmail(trimmedEmail);
    }

    private void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("Password hash is required.");

        PasswordHash = passwordHash.Trim();
    }

    private void SetProfile(string firstName, string lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new DomainException("First name is required.");

        var trimmedFirst = firstName.Trim();
        if (trimmedFirst.Length > FirstNameMaxLength)
            throw new DomainException($"First name cannot exceed {FirstNameMaxLength} characters.");

        if (string.IsNullOrWhiteSpace(lastName))
            throw new DomainException("Last name is required.");

        var trimmedLast = lastName.Trim();
        if (trimmedLast.Length > LastNameMaxLength)
            throw new DomainException($"Last name cannot exceed {LastNameMaxLength} characters.");

        FirstName = trimmedFirst;
        LastName = trimmedLast;
    }

    private void EnsureActive()
    {
        if (!IsActive)
            throw new DomainException("Inactive user cannot be modified.");
    }
}

