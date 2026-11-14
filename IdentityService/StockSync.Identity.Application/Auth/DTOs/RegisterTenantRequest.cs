namespace StockSync.Identity.Application.Auth.DTOs;

public record RegisterTenantRequest(
    string CompanyName,
    string? TaxId,
    string FirstName,
    string LastName,
    string Email,
    string Password);

