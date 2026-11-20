using StockSync.Reporting.Application;
using StockSync.Reporting.Application.Common.Exceptions;

namespace StockSync.Reporting.UnitTests;

public class FilterTests
{
    [Fact]
    public void DefaultsUseTheSelectedEnd()
    {
        var now = DateTime.UtcNow;
        var filter = SalesFilter.Create(null, null, null, now);
        Assert.Equal(now, filter.To);
        Assert.Equal(now.AddDays(-30), filter.From);
        var earlier = SalesFilter.Create(null, "2025-09-01", null, now);
        Assert.Equal(earlier.To.AddDays(-30), earlier.From);
    }

    [Theory]
    [InlineData("2025-01-01", "2024-01-01")]
    [InlineData("2024-01-01", "2025-01-02")]
    [InlineData("invalid", null)]
    [InlineData("2025-01-01T04:00:00+04:00", null)]
    public void RejectsInvalidDateRange(string from, string? to) =>
        Assert.Throws<ValidationException>(() => SalesFilter.Create(from, to, null, DateTime.UtcNow));

    [Fact]
    public void EnforcesBranchPageAndLimitBounds()
    {
        Assert.Throws<ValidationException>(() => SalesFilter.Create(null, null, Guid.Empty, DateTime.UtcNow));
        Assert.Throws<ValidationException>(() => SalesFilter.ValidateLimit(0));
        Assert.Throws<ValidationException>(() => SalesFilter.ValidateLimit(51));
        Assert.Throws<ValidationException>(() => SalesFilter.ValidatePage(0, 20));
        Assert.Throws<ValidationException>(() => SalesFilter.ValidatePage(1, 101));
    }
}
