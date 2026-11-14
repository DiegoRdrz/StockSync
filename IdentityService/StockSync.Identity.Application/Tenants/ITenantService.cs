using StockSync.Identity.Application.Tenants.DTOs;

namespace StockSync.Identity.Application.Tenants;

public interface ITenantService
{
    Task<TenantResponse> GetCurrentTenantAsync(CancellationToken cancellationToken = default);
    Task<TenantResponse> UpdateCurrentTenantAsync(UpdateTenantRequest request, CancellationToken cancellationToken = default);
}

