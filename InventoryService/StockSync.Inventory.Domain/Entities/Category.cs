using StockSync.Inventory.Domain.Common;
using StockSync.Inventory.Domain.Exceptions;

namespace StockSync.Inventory.Domain.Entities;

public class Category
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 300;

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    // Persist the normalized name so the unique index uses the application's comparison.
    public string NormalizedName { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Category()
    {
    }

    public static Category Create(string name, string? description)
    {
        var category = new Category
        {
            Id = Guid.NewGuid(),
            IsActive = true,
            CreatedAt = UtcClock.Now()
        };
        category.SetDetails(name, description);
        return category;
    }

    public void Update(string name, string? description)
    {
        EnsureActive();
        SetDetails(name, description);
        UpdatedAt = UtcClock.Now();
    }

    public void Deactivate()
    {
        EnsureActive();
        IsActive = false;
        UpdatedAt = UtcClock.Now();
    }

    // Normalize names so "Tools" and "  tools " refer to the same category.
    public static string NormalizeName(string name) => name.Trim().ToUpperInvariant();

    private void SetDetails(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Category name is required.");
        if (name.Trim().Length > NameMaxLength)
            throw new DomainException($"Name cannot exceed {NameMaxLength} characters.");
        if (description?.Trim().Length > DescriptionMaxLength)
            throw new DomainException($"Description cannot exceed {DescriptionMaxLength} characters.");

        Name = name.Trim();
        NormalizedName = NormalizeName(name);
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    private void EnsureActive()
    {
        if (!IsActive)
            throw new DomainException("The category is inactive.");
    }
}
