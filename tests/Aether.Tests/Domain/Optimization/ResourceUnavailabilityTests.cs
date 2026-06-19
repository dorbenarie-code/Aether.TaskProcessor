using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ResourceUnavailabilityTests
{
    [Fact]
    public void Resource_unavailability_requires_non_empty_resource_id()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new ResourceUnavailability(
                Guid.Empty,
                new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 1, 16, 0, 0, DateTimeKind.Utc)));

        Assert.Equal("resourceId", exception.ParamName);
    }

    [Fact]
    public void Resource_unavailability_requires_utc_start_time()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new ResourceUnavailability(
                Guid.NewGuid(),
                DateTime.Now,
                new DateTime(2026, 1, 1, 16, 0, 0, DateTimeKind.Utc)));

        Assert.Equal("startUtc", exception.ParamName);
    }

    [Fact]
    public void Resource_unavailability_requires_utc_end_time()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new ResourceUnavailability(
                Guid.NewGuid(),
                new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc),
                DateTime.Now));

        Assert.Equal("endUtc", exception.ParamName);
    }

    [Fact]
    public void Resource_unavailability_requires_end_after_start()
    {
        var startUtc = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);

        var exception = Assert.Throws<ArgumentException>(() =>
            new ResourceUnavailability(
                Guid.NewGuid(),
                startUtc,
                startUtc));

        Assert.Equal("endUtc", exception.ParamName);
    }

    [Fact]
    public void Resource_unavailability_exposes_values()
    {
        var resourceId = Guid.NewGuid();
        var startUtc = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var endUtc = new DateTime(2026, 1, 1, 16, 0, 0, DateTimeKind.Utc);

        var unavailability = new ResourceUnavailability(
            resourceId,
            startUtc,
            endUtc);

        Assert.Equal(resourceId, unavailability.ResourceId);
        Assert.Equal(startUtc, unavailability.StartUtc);
        Assert.Equal(endUtc, unavailability.EndUtc);
    }
}
