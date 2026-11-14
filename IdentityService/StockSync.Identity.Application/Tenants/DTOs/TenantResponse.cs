using StockSync.Identity.Domain.Entities;

namespace StockSync.Identity.Application.Tenants.DTOs;

public record TenantResponse(
    Guid Id,
    string Name,
    string? TaxId,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public static TenantResponse From(Tenant tenant) =>
        new(tenant.Id, tenant.Name, tenant.TaxId, tenant.IsActive, tenant.CreatedAt, tenant.UpdatedAt);
}

