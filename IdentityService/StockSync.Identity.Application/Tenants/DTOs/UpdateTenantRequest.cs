namespace StockSync.Identity.Application.Tenants.DTOs;

public record UpdateTenantRequest(
    string Name,
    string? TaxId);

