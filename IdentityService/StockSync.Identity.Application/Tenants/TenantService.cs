using StockSync.Identity.Application.Common;
using StockSync.Identity.Application.Common.Exceptions;
using StockSync.Identity.Application.Tenants.DTOs;
using StockSync.Identity.Domain.Repositories;

namespace StockSync.Identity.Application.Tenants;

public class TenantService : ITenantService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenant _currentTenant;

    public TenantService(
        ITenantRepository tenantRepository,
        IUnitOfWork unitOfWork,
        ICurrentTenant currentTenant)
    {
        _tenantRepository = tenantRepository;
        _unitOfWork = unitOfWork;
        _currentTenant = currentTenant;
    }

    public async Task<TenantResponse> GetCurrentTenantAsync(CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(_currentTenant.TenantId, cancellationToken)
            ?? throw new NotFoundException($"Tenant with ID '{_currentTenant.TenantId}' was not found.");

        return TenantResponse.From(tenant);
    }

    public async Task<TenantResponse> UpdateCurrentTenantAsync(UpdateTenantRequest request, CancellationToken cancellationToken = default)
    {
        ThrowIfErrors(TenantValidator.Validate(request));

        var tenant = await _tenantRepository.GetByIdAsync(_currentTenant.TenantId, cancellationToken)
            ?? throw new NotFoundException($"Tenant with ID '{_currentTenant.TenantId}' was not found.");

        if (await _tenantRepository.ExistsByNameAsync(request.Name, tenant.Id, cancellationToken))
            throw new ConflictException($"A company with name '{request.Name}' already exists.");

        tenant.Update(request.Name, request.TaxId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return TenantResponse.From(tenant);
    }

    private static void ThrowIfErrors(Dictionary<string, string[]> errors)
    {
        if (errors.Count > 0)
            throw new ValidationException(errors);
    }
}

