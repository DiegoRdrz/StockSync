using Microsoft.EntityFrameworkCore;
using StockSync.Identity.Application.Common;
using StockSync.Identity.Application.Common.Interfaces;
using StockSync.Identity.Domain.Entities;

namespace StockSync.Identity.Infrastructure;

public static class IdentityDbSeeder
{
    public const string DemoOwnerEmail = "owner@stocksync.demo";
    public const string DemoEmployeeEmail = "employee@stocksync.demo";
    public const string DefaultDemoPassword = "Password123!";

    public static async Task SeedAsync(IdentityDbContext context, IPasswordHasher passwordHasher)
    {
        var defaultTenantId = DefaultTenant.Id;

        var existingTenant = await context.Tenants
            .FirstOrDefaultAsync(t => t.Id == defaultTenantId);

        if (existingTenant is null)
        {
            existingTenant = Tenant.Create(
                name: "StockSync Demo Hardware Store",
                taxId: "DEMO-TAX-001",
                id: defaultTenantId);

            await context.Tenants.AddAsync(existingTenant);
        }

        var normalizedOwnerEmail = User.NormalizeEmail(DemoOwnerEmail);
        var existingOwner = await context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedOwnerEmail);

        if (existingOwner is null)
        {
            var ownerUser = User.Create(
                tenantId: defaultTenantId,
                email: DemoOwnerEmail,
                passwordHash: passwordHasher.HashPassword(DefaultDemoPassword),
                firstName: "Demo",
                lastName: "Owner",
                role: RoleType.Owner,
                id: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

            await context.Users.AddAsync(ownerUser);
        }

        var normalizedEmployeeEmail = User.NormalizeEmail(DemoEmployeeEmail);
        var existingEmployee = await context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmployeeEmail);

        if (existingEmployee is null)
        {
            var employeeUser = User.Create(
                tenantId: defaultTenantId,
                email: DemoEmployeeEmail,
                passwordHash: passwordHasher.HashPassword(DefaultDemoPassword),
                firstName: "Demo",
                lastName: "Employee",
                role: RoleType.Employee,
                id: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

            await context.Users.AddAsync(employeeUser);
        }

        await context.SaveChangesAsync();
    }
}

