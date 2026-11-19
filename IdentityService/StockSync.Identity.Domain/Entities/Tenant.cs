using StockSync.Identity.Domain.Common;
using StockSync.Identity.Domain.Exceptions;

namespace StockSync.Identity.Domain.Entities;

public class Tenant
{
    public const int NameMaxLength = 100;
    public const int TaxIdMaxLength = 50;

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string? TaxId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Tenant()
    {
    }

    public static Tenant Create(string name, string? taxId = null, Guid? id = null)
    {
        var tenant = new Tenant
        {
            Id = id ?? Guid.NewGuid(),
            IsActive = true,
            CreatedAt = ClockUtc.Now()
        };

        tenant.SetDetails(name, taxId);
        return tenant;
    }

    public void Update(string name, string? taxId)
    {
        EnsureActive();
        SetDetails(name, taxId);
        UpdatedAt = ClockUtc.Now();
    }

    public void Deactivate()
    {
        EnsureActive();
        IsActive = false;
        UpdatedAt = ClockUtc.Now();
    }

    public static string NormalizeName(string name) => name.Trim().ToUpperInvariant();

    private void SetDetails(string name, string? taxId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Tenant name is required.");

        var trimmedName = name.Trim();
        if (trimmedName.Length > NameMaxLength)
            throw new DomainException($"Tenant name cannot exceed {NameMaxLength} characters.");

        var trimmedTaxId = string.IsNullOrWhiteSpace(taxId) ? null : taxId.Trim();
        if (trimmedTaxId?.Length > TaxIdMaxLength)
            throw new DomainException($"Tax identification number cannot exceed {TaxIdMaxLength} characters.");

        Name = trimmedName;
        TaxId = trimmedTaxId;
    }

    private void EnsureActive()
    {
        if (!IsActive)
            throw new DomainException("Inactive tenant cannot be modified.");
    }
}

