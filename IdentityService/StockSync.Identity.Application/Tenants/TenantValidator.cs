using StockSync.Identity.Application.Tenants.DTOs;
using StockSync.Identity.Domain.Entities;

namespace StockSync.Identity.Application.Tenants;

public static class TenantValidator
{
    public static Dictionary<string, string[]> Validate(UpdateTenantRequest request)
    {
        var errors = new Dictionary<string, List<string>>();

        if (string.IsNullOrWhiteSpace(request.Name))
            Add(errors, nameof(request.Name), "Tenant name is required.");
        else if (request.Name.Trim().Length > Tenant.NameMaxLength)
            Add(errors, nameof(request.Name), $"Tenant name cannot exceed {Tenant.NameMaxLength} characters.");

        if (request.TaxId?.Trim().Length > Tenant.TaxIdMaxLength)
            Add(errors, nameof(request.TaxId), $"Tax identification number cannot exceed {Tenant.TaxIdMaxLength} characters.");

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

