using StockSync.Identity.Domain.Entities;

namespace StockSync.Identity.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user, Tenant tenant);
    int ExpirationMinutes { get; }
}

