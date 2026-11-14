using StockSync.Inventory.Domain.Common;
using StockSync.Inventory.Domain.Exceptions;

namespace StockSync.Inventory.Domain.Entities;

public class Product
{
    public const int NameMaxLength = 150;
    public const int SkuMaxLength = 50;
    public const int DescriptionMaxLength = 500;
    // Prices use numeric(18,2): extra decimals would be rounded and extra digits would overflow.
    public const int PriceDecimalPlaces = 2;
    public const decimal MaxPrice = 9_999_999_999_999_999.99m;

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Sku { get; private set; } = null!;
    public string? Description { get; private set; }
    public decimal PurchasePrice { get; private set; }
    public decimal SalePrice { get; private set; }
    public int MinimumStock { get; private set; }
    public Guid? CategoryId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Product()
    {
    }

    public static Product Create(
        string name,
        string sku,
        string? description,
        decimal purchasePrice,
        decimal salePrice,
        int minimumStock,
        Guid? categoryId)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            IsActive = true,
            CreatedAt = UtcClock.Now()
        };
        product.SetDetails(name, sku, description, purchasePrice, salePrice, minimumStock, categoryId);
        return product;
    }

    public void Update(
        string name,
        string sku,
        string? description,
        decimal purchasePrice,
        decimal salePrice,
        int minimumStock,
        Guid? categoryId)
    {
        EnsureActive();
        SetDetails(name, sku, description, purchasePrice, salePrice, minimumStock, categoryId);
        UpdatedAt = UtcClock.Now();
    }

    public void Deactivate()
    {
        EnsureActive();
        IsActive = false;
        UpdatedAt = UtcClock.Now();
    }

    // Normalize SKUs so "abc-1" and " ABC-1 " cannot coexist.
    public static string NormalizeSku(string sku) => sku.Trim().ToUpperInvariant();

    public static bool IsRepresentablePrice(decimal price) =>
        price <= MaxPrice && decimal.Round(price, PriceDecimalPlaces) == price;

    private void SetDetails(
        string name,
        string sku,
        string? description,
        decimal purchasePrice,
        decimal salePrice,
        int minimumStock,
        Guid? categoryId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Product name is required.");
        if (name.Trim().Length > NameMaxLength)
            throw new DomainException($"Name cannot exceed {NameMaxLength} characters.");
        if (string.IsNullOrWhiteSpace(sku))
            throw new DomainException("Product SKU is required.");
        if (sku.Trim().Length > SkuMaxLength)
            throw new DomainException($"SKU cannot exceed {SkuMaxLength} characters.");
        if (description?.Trim().Length > DescriptionMaxLength)
            throw new DomainException($"Description cannot exceed {DescriptionMaxLength} characters.");
        if (purchasePrice < 0)
            throw new DomainException("Purchase price cannot be negative.");
        if (!IsRepresentablePrice(purchasePrice))
            throw new DomainException($"Purchase price allows at most {PriceDecimalPlaces} decimal places and cannot exceed {MaxPrice}.");
        if (salePrice < 0)
            throw new DomainException("Sale price cannot be negative.");
        if (!IsRepresentablePrice(salePrice))
            throw new DomainException($"Sale price allows at most {PriceDecimalPlaces} decimal places and cannot exceed {MaxPrice}.");
        if (minimumStock < 0)
            throw new DomainException("Minimum stock cannot be negative.");
        if (categoryId == Guid.Empty)
            throw new DomainException("The specified category is invalid.");

        Name = name.Trim();
        Sku = NormalizeSku(sku);
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        PurchasePrice = purchasePrice;
        SalePrice = salePrice;
        MinimumStock = minimumStock;
        CategoryId = categoryId;
    }

    private void EnsureActive()
    {
        if (!IsActive)
            throw new DomainException("The product is inactive.");
    }
}
