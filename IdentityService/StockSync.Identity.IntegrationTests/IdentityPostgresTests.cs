using Microsoft.EntityFrameworkCore;
using StockSync.Identity.Domain.Entities;
using StockSync.Identity.Infrastructure.Repositories;
using Xunit;

namespace StockSync.Identity.IntegrationTests;

public class IdentityPostgresTests : PostgresTestBase
{
    [PostgreSqlFact]
    public async Task TenantAndUser_PersistenceAndRetrieval_ShouldWork()
    {
        await using var context = CreateContext();
        var tenantRepo = new TenantRepository(context);
        var userRepo = new UserRepository(context);

        var tenant = Tenant.Create("Integration Test Company", "TAX-INTEG-001");
        await tenantRepo.AddAsync(tenant);

        var user = User.Create(
            tenant.Id,
            "admin@integ.com",
            "hashed_secret",
            "Admin",
            "User",
            RoleType.Owner);
        await userRepo.AddAsync(user);

        await context.SaveChangesAsync();

        var retrievedTenant = await tenantRepo.GetByIdAsync(tenant.Id);
        Assert.NotNull(retrievedTenant);
        Assert.Equal("Integration Test Company", retrievedTenant.Name);

        var retrievedUser = await userRepo.GetByEmailAsync("admin@integ.com");
        Assert.NotNull(retrievedUser);
        Assert.Equal(tenant.Id, retrievedUser.TenantId);
        Assert.Equal(RoleType.Owner, retrievedUser.Role);
    }

    [PostgreSqlFact]
    public async Task UniqueEmailConstraint_ShouldPreventDuplicatesAcrossActiveUsers()
    {
        await using var context = CreateContext();
        var tenant = Tenant.Create("Company A");
        await context.Tenants.AddAsync(tenant);
        await context.SaveChangesAsync();

        var user1 = User.Create(tenant.Id, "shared@integ.com", "hash1", "User", "One", RoleType.Employee);
        await context.Users.AddAsync(user1);
        await context.SaveChangesAsync();

        var user2 = User.Create(tenant.Id, "shared@integ.com", "hash2", "User", "Two", RoleType.Employee);
        await context.Users.AddAsync(user2);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}

